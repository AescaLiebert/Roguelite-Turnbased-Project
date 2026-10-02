using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FightingAllstar.Contracts.Protocol;
using FightingAllstar.Contracts.Projections;
using FightingAllstar.Contracts.Security;
using FightingAllstar.Core.Combat;

namespace FightingAllstar.Server.Application.Battle
{
    public sealed class BattleCommandResult
    {
        public bool Succeeded;
        public BattleView View;
        public CommandError Error;

        public static BattleCommandResult Success(BattleView view) => new BattleCommandResult { Succeeded = true, View = view };
        public static BattleCommandResult Failure(string code, string message, long revision = 0) =>
            new BattleCommandResult { Error = new CommandError { Code = code, Message = message, CurrentRevision = revision } };
    }

    /// <summary>Applies authenticated player plans through the shared Core and stores each result exactly once.</summary>
    public sealed class BattleCommandService
    {
        private readonly IBattleCommandRepository _repository;
        private readonly ITokenIdentityValidator _identityValidator;
        private readonly IServerClock _clock;

        public BattleCommandService(IBattleCommandRepository repository, ITokenIdentityValidator identityValidator, IServerClock clock)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _identityValidator = identityValidator ?? throw new ArgumentNullException(nameof(identityValidator));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task<BattleCommandResult> SubmitPlanAsync(string bearerToken, string matchId,
            CommandEnvelope<SubmitBattlePlan> envelope, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
                return BattleCommandResult.Failure("unauthenticated", "A verified account identity is required.");
            var identity = await _identityValidator.ValidateAsync(bearerToken, cancellationToken).ConfigureAwait(false);
            if (identity == null || string.IsNullOrWhiteSpace(identity.SubjectId)
                || identity.ExpiresAtUnixSeconds <= _clock.UtcNowUnixSeconds)
                return BattleCommandResult.Failure("unauthenticated", "The supplied identity token is invalid or expired.");
            if (string.IsNullOrWhiteSpace(matchId))
                return BattleCommandResult.Failure("invalid_match", "A match ID is required.");
            if (envelope == null || envelope.SchemaVersion != 1 || string.IsNullOrWhiteSpace(envelope.RequestId))
                return BattleCommandResult.Failure("invalid_command", "A supported command envelope and request ID are required.");
            if (envelope.Payload == null || envelope.Payload.Actions == null)
                return BattleCommandResult.Failure("invalid_command", "A battle plan is required.");

            var payloadHash = CommandFingerprint.Compute("battle.submit-plan", envelope.RequestId,
                envelope.ExpectedRevision, Canonicalize(envelope.Payload));
            var snapshot = await _repository.ReadAsync(matchId, identity.SubjectId, envelope.RequestId, cancellationToken)
                .ConfigureAwait(false);
            if (snapshot == null || snapshot.State == null)
                return BattleCommandResult.Failure("match_not_found", "No active match was found.");

            var side = GetMemberSide(snapshot, identity.SubjectId);
            if (!side.HasValue)
                return BattleCommandResult.Failure("forbidden", "This account is not a member of the match.");

            if (snapshot.ExistingReceipt != null)
            {
                if (!string.Equals(snapshot.ExistingReceipt.PayloadHash, payloadHash, StringComparison.Ordinal))
                    return BattleCommandResult.Failure("idempotency_conflict", "This request ID was already used with a different command.", snapshot.State.Revision);
                return BattleCommandResult.Success(snapshot.ExistingReceipt.Result);
            }

            if (snapshot.State.ActingSide != side.Value)
                return BattleCommandResult.Failure("not_your_turn", "The match is waiting for the other player.", snapshot.State.Revision);
            if (snapshot.State.Revision != envelope.ExpectedRevision)
                return BattleCommandResult.Failure("revision_conflict", "The battle has changed since this plan was prepared.", snapshot.State.Revision);

            var plan = new TurnPlan { RequestId = envelope.RequestId, ExpectedRevision = envelope.ExpectedRevision };
            foreach (var action in envelope.Payload.Actions)
            {
                if (action == null) return BattleCommandResult.Failure("invalid_command", "The plan contains an empty action.", snapshot.State.Revision);
                plan.Actions.Add(new PlannedAction
                {
                    IsMove = action.IsMove,
                    CardId = action.CardId,
                    TargetFighterId = action.TargetFighterId,
                    DestinationIndex = action.DestinationIndex
                });
            }

            if (!BattleEngine.TryResolvePlan(snapshot.State, plan, out var nextState, out var error))
                return BattleCommandResult.Failure("illegal_plan", error, snapshot.State.Revision);

            var view = BattleProjectionBuilder.Build(nextState, side.Value, identity.SubjectId);
            var receipt = new CommandReceipt<BattleView>
            {
                RequestId = envelope.RequestId,
                PayloadHash = payloadHash,
                ResultRevision = nextState.Revision,
                Result = view
            };
            var committed = await _repository.TryCommitAsync(matchId, identity.SubjectId, snapshot.StorageVersion,
                nextState, receipt, cancellationToken).ConfigureAwait(false);
            if (committed) return BattleCommandResult.Success(view);

            // Another request may have won the compare-and-swap. Reload to distinguish a safe retry from a stale plan.
            var latest = await _repository.ReadAsync(matchId, identity.SubjectId, envelope.RequestId, cancellationToken)
                .ConfigureAwait(false);
            if (latest == null || !GetMemberSide(latest, identity.SubjectId).HasValue)
                return BattleCommandResult.Failure("forbidden", "This account is not a member of the match.");
            if (latest?.ExistingReceipt != null)
            {
                if (string.Equals(latest.ExistingReceipt.PayloadHash, payloadHash, StringComparison.Ordinal))
                    return BattleCommandResult.Success(latest.ExistingReceipt.Result);
                return BattleCommandResult.Failure("idempotency_conflict", "This request ID was already used with a different command.", latest.State?.Revision ?? 0);
            }
            return BattleCommandResult.Failure("revision_conflict", "The battle changed before this command could be saved.", latest?.State?.Revision ?? snapshot.State.Revision);
        }

        private static TeamSide? GetMemberSide(BattleCommandSnapshot snapshot, string subjectId)
        {
            if (string.Equals(snapshot.PlayerSubjectId, subjectId, StringComparison.Ordinal)) return TeamSide.Player;
            if (string.Equals(snapshot.OpponentSubjectId, subjectId, StringComparison.Ordinal)) return TeamSide.Opponent;
            return null;
        }

        private static string Canonicalize(SubmitBattlePlan payload)
        {
            var builder = new StringBuilder();
            builder.Append(payload.Actions.Count.ToString(CultureInfo.InvariantCulture)).Append('|');
            foreach (var action in payload.Actions)
            {
                if (action == null) { builder.Append("null|"); continue; }
                builder.Append(action.IsMove ? '1' : '0').Append('|');
                Append(builder, action.CardId);
                Append(builder, action.TargetFighterId);
                builder.Append(action.DestinationIndex.ToString(CultureInfo.InvariantCulture)).Append('|');
            }
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('|');
        }
    }
}
