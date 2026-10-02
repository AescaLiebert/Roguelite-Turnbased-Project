using System;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Economy
{
    public enum SummonRarity { R, SR, SSR }
    public enum CharacterGrantKind { NewCharacter, DuplicateCrest, CollectionToken }

    [Serializable]
    public sealed class BannerPoolEntry
    {
        public string CharacterId;
        public SummonRarity Rarity;
        public int IndividualRateBp;
    }

    [Serializable]
    public sealed class BannerDefinition
    {
        public string Id;
        public string Revision;
        public string DisplayName;
        public string SeriesId;
        public string GuaranteeGroupId;
        public string GuaranteePolicyId;
        public int SinglePullPrice = 160;
        public int TenPullPrice = 1600;
        public int SSRRateBp = 400;
        public int SRRateBp = 3600;
        public int RRateBp = 6000;
        public List<BannerPoolEntry> Pool = new List<BannerPoolEntry>();

        public List<string> FeaturedIds()
        {
            var ids = new List<string>();
            if (Pool != null) foreach (var entry in Pool)
                if (entry != null && entry.Rarity == SummonRarity.SSR) ids.Add(entry.CharacterId);
            return ids;
        }

        public List<string> Validate(ContentCatalog catalog, bool requireRuntimeReady = true)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Revision)) errors.Add("Banner identity is incomplete.");
            if (SinglePullPrice != 160 || TenPullPrice != 1600) errors.Add("KOF prototype prices must be 160/1600 Diamonds.");
            if (SSRRateBp != 400 || SRRateBp != 3600 || RRateBp != 6000) errors.Add("KOF prototype rarity rates must be 4%/36%/60%.");
            if (Pool == null || Pool.Count == 0) { errors.Add("Banner pool is empty."); return errors; }
            var rates = new int[3];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in Pool)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.CharacterId)) { errors.Add("Banner contains an incomplete pool entry."); continue; }
                if (entry.Rarity < SummonRarity.R || entry.Rarity > SummonRarity.SSR) { errors.Add(entry.CharacterId + " has an invalid rarity bucket."); continue; }
                rates[(int)entry.Rarity] += entry.IndividualRateBp;
                if (entry.IndividualRateBp <= 0) errors.Add(entry.CharacterId + " has a non-positive individual rate.");
                if (!ids.Add(entry.CharacterId)) errors.Add("Banner contains duplicate fighter " + entry.CharacterId + ".");
                var fighter = catalog?.Characters?.Find(x => x != null && x.Id == entry.CharacterId);
                if (fighter == null) errors.Add("Banner fighter is missing from the catalog: " + entry.CharacterId + ".");
                else
                {
                    if (fighter.SeriesId != SeriesId) errors.Add(entry.CharacterId + " is outside banner series " + SeriesId + ".");
                    if (fighter.RarityId != "rarity." + entry.Rarity.ToString().ToLowerInvariant()) errors.Add(entry.CharacterId + " rarity does not match its disclosed bucket.");
                    if (requireRuntimeReady && !fighter.RuntimeReady) errors.Add(entry.CharacterId + " is draft-only and cannot enter the playable banner.");
                }
            }
            if (rates[0] != RRateBp || rates[1] != SRRateBp || rates[2] != SSRRateBp)
                errors.Add("Individual rates do not sum to the disclosed rarity rates.");
            if (rates[0] + rates[1] + rates[2] != 10000) errors.Add("Banner rates must sum to 100%.");
            if (FeaturedIds().Count == 0) errors.Add("Banner needs at least one featured SSR for its selector.");
            return errors;
        }

        public static BannerDefinition CreateKofPhaseE()
        {
            return new BannerDefinition
            {
                Id = "banner.kof.phase-e.local", Revision = "kof-phase-e-local-v1", DisplayName = "KOF Fighters",
                SeriesId = "series.kof", GuaranteeGroupId = "guarantee.kof", GuaranteePolicyId = "policy.selector-300-proposed-v1",
                Pool = new List<BannerPoolEntry>
                {
                    Entry("fighter.athena94", SummonRarity.SSR, 400),
                    Entry("fighter.kyo94", SummonRarity.SR, 900), Entry("fighter.king94", SummonRarity.SR, 900),
                    Entry("fighter.mai94", SummonRarity.SR, 900), Entry("fighter.shingo97", SummonRarity.SR, 900),
                    Entry("fighter.chin94", SummonRarity.R, 2000), Entry("fighter.kensou94", SummonRarity.R, 2000),
                    Entry("fighter.benimaru94", SummonRarity.R, 2000)
                }
            };
        }

        private static BannerPoolEntry Entry(string id, SummonRarity rarity, int rate) =>
            new BannerPoolEntry { CharacterId = id, Rarity = rarity, IndividualRateBp = rate };
    }

    [Serializable]
    public sealed class LocalOwnedCharacter
    {
        public string DefinitionId;
        public int ConstellationTier;
        public int CrestCount;
        public LocalOwnedCharacter Clone() => (LocalOwnedCharacter)MemberwiseClone();
    }

    [Serializable]
    public sealed class LocalGuaranteeProgress
    {
        public string GroupId;
        public int Pulls;
        public LocalGuaranteeProgress Clone() => (LocalGuaranteeProgress)MemberwiseClone();
    }

    [Serializable]
    public sealed class LocalSelectorEntitlement
    {
        public string Id;
        public string GroupId;
        public string BannerRevision;
        public List<string> EligibleCharacterIds = new List<string>();
        public bool Claimed;
        public string ClaimRequestId;
        public string ClaimedCharacterId;
        public LocalSelectorEntitlement Clone()
        {
            var copy = (LocalSelectorEntitlement)MemberwiseClone();
            copy.EligibleCharacterIds = EligibleCharacterIds == null ? new List<string>() : new List<string>(EligibleCharacterIds);
            return copy;
        }
    }

    [Serializable]
    public sealed class CharacterGrantOutcome
    {
        public string CharacterId;
        public CharacterGrantKind Kind;
        public int ConstellationTier;
        public int CrestCount;
        public int CollectionTokens;
        public CharacterGrantOutcome Clone() => (CharacterGrantOutcome)MemberwiseClone();
    }

    [Serializable]
    public sealed class SummonDraw
    {
        public string CharacterId;
        public SummonRarity Rarity;
        public CharacterGrantOutcome Grant;
        public SummonDraw Clone() => new SummonDraw { CharacterId = CharacterId, Rarity = Rarity, Grant = Grant?.Clone() };
    }

    [Serializable]
    public sealed class SummonReceipt
    {
        public string RequestId;
        public string BannerId;
        public string BannerRevision;
        public int Count;
        public int Debit;
        public int BalanceBefore;
        public int BalanceAfter;
        public int GuaranteeBefore;
        public int GuaranteeAfter;
        public int SelectorsEarned;
        public List<SummonDraw> Draws = new List<SummonDraw>();
        public SummonReceipt Clone()
        {
            var copy = (SummonReceipt)MemberwiseClone();
            copy.Draws = new List<SummonDraw>();
            if (Draws != null) foreach (var draw in Draws) copy.Draws.Add(draw?.Clone());
            return copy;
        }
    }

    [Serializable]
    public sealed class LocalEconomyLedgerEntry
    {
        public string OperationId;
        public string Kind;
        public int Amount;
        public int BalanceAfter;
        public LocalEconomyLedgerEntry Clone() => (LocalEconomyLedgerEntry)MemberwiseClone();
    }

    [Serializable]
    public sealed class LocalEconomyProgressionReceipt
    {
        public string RequestId;
        public string CharacterId;
        public string EntitlementId;
        public int TierBefore;
        public int TierAfter;
        public CharacterGrantOutcome Grant;
        public LocalEconomyProgressionReceipt Clone() => new LocalEconomyProgressionReceipt { RequestId = RequestId,
            CharacterId = CharacterId, EntitlementId = EntitlementId, TierBefore = TierBefore, TierAfter = TierAfter, Grant = Grant?.Clone() };
    }

    [Serializable]
    public sealed class LocalEconomyState
    {
        public int SchemaVersion = 1;
        public string SubjectId;
        public int Diamonds;
        public int CollectionTokens;
        public long Revision;
        public string RandomState = "1";
        public long RandomDrawCount;
        public List<LocalOwnedCharacter> Roster = new List<LocalOwnedCharacter>();
        public List<string> FormationDefinitionIds = new List<string>();
        public List<LocalGuaranteeProgress> GuaranteeProgress = new List<LocalGuaranteeProgress>();
        public List<LocalSelectorEntitlement> Selectors = new List<LocalSelectorEntitlement>();
        public List<SummonReceipt> SummonReceipts = new List<SummonReceipt>();
        public List<LocalEconomyLedgerEntry> Ledger = new List<LocalEconomyLedgerEntry>();
        public List<LocalEconomyProgressionReceipt> ProgressionReceipts = new List<LocalEconomyProgressionReceipt>();

        public static LocalEconomyState CreateLocalProfile(string subjectId, int initialDiamonds = 1600, ulong seed = 1)
        {
            if (initialDiamonds < 0) throw new ArgumentOutOfRangeException(nameof(initialDiamonds));
            return new LocalEconomyState { SubjectId = subjectId, Diamonds = initialDiamonds,
                RandomState = seed.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        }

        public LocalEconomyState Clone()
        {
            var copy = new LocalEconomyState { SchemaVersion = SchemaVersion, SubjectId = SubjectId, Diamonds = Diamonds,
                CollectionTokens = CollectionTokens, Revision = Revision, RandomState = RandomState, RandomDrawCount = RandomDrawCount };
            if (Roster != null) foreach (var item in Roster) copy.Roster.Add(item?.Clone());
            if (FormationDefinitionIds != null) copy.FormationDefinitionIds.AddRange(FormationDefinitionIds);
            if (GuaranteeProgress != null) foreach (var item in GuaranteeProgress) copy.GuaranteeProgress.Add(item?.Clone());
            if (Selectors != null) foreach (var item in Selectors) copy.Selectors.Add(item?.Clone());
            if (SummonReceipts != null) foreach (var item in SummonReceipts) copy.SummonReceipts.Add(item?.Clone());
            if (Ledger != null) foreach (var item in Ledger) copy.Ledger.Add(item?.Clone());
            if (ProgressionReceipts != null) foreach (var item in ProgressionReceipts) copy.ProgressionReceipts.Add(item?.Clone());
            return copy;
        }
    }

    /// <summary>Disposable offline economy simulation. It grants no online entitlement or trusted currency.</summary>
    public sealed class LocalEconomyService
    {
        private readonly LocalEconomyState _state;
        private readonly BannerDefinition _banner;
        private readonly ContentCatalog _catalog;
        private readonly DeterministicRandom _random;

        public LocalEconomyState State => _state;
        public BannerDefinition Banner => _banner;

        public LocalEconomyService(LocalEconomyState state, BannerDefinition banner, ContentCatalog catalog)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _banner = banner ?? throw new ArgumentNullException(nameof(banner));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            var errors = banner.Validate(catalog);
            if (errors.Count > 0) throw new InvalidOperationException("Local banner is invalid: " + string.Join("; ", errors));
            if (_state.SchemaVersion != 1) throw new InvalidOperationException("Unsupported local economy save version.");
            _state.Roster = _state.Roster ?? new List<LocalOwnedCharacter>();
            _state.FormationDefinitionIds = _state.FormationDefinitionIds ?? new List<string>();
            _state.GuaranteeProgress = _state.GuaranteeProgress ?? new List<LocalGuaranteeProgress>();
            _state.Selectors = _state.Selectors ?? new List<LocalSelectorEntitlement>();
            _state.SummonReceipts = _state.SummonReceipts ?? new List<SummonReceipt>();
            _state.Ledger = _state.Ledger ?? new List<LocalEconomyLedgerEntry>();
            _state.ProgressionReceipts = _state.ProgressionReceipts ?? new List<LocalEconomyProgressionReceipt>();
            if (!ulong.TryParse(_state.RandomState, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var randomState)) randomState = 1;
            if (randomState == 0) randomState = 1;
            _random = DeterministicRandom.Restore(randomState, (ulong)Math.Max(0, _state.RandomDrawCount));
        }

        public int GuaranteePulls
        {
            get { var progress = FindProgress(); return progress?.Pulls ?? 0; }
        }

        public int UnclaimedSelectors
        {
            get { var count = 0; foreach (var selector in _state.Selectors) if (selector != null && !selector.Claimed) count++; return count; }
        }

        public SummonReceipt Summon(string requestId, int count)
        {
            if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("A summon request ID is required.", nameof(requestId));
            if (count != 1 && count != 10) throw new ArgumentOutOfRangeException(nameof(count), "Only single and ten-pull requests are supported.");
            var old = _state.SummonReceipts.Find(x => x != null && x.RequestId == requestId);
            if (old != null)
            {
                if (old.Count != count || old.BannerId != _banner.Id) throw new InvalidOperationException("Request ID was already used for a different summon payload.");
                return old.Clone();
            }
            var debit = count == 1 ? _banner.SinglePullPrice : _banner.TenPullPrice;
            if (_state.Diamonds < debit) throw new InvalidOperationException("Insufficient Diamonds. Need " + debit + ", have " + _state.Diamonds + ".");
            var progress = FindOrCreateProgress();
            var receipt = new SummonReceipt { RequestId = requestId, BannerId = _banner.Id, BannerRevision = _banner.Revision,
                Count = count, Debit = debit, BalanceBefore = _state.Diamonds, GuaranteeBefore = progress.Pulls };
            _state.Diamonds -= debit;
            for (var i = 0; i < count; i++)
            {
                var entry = DrawEntry();
                receipt.Draws.Add(new SummonDraw { CharacterId = entry.CharacterId, Rarity = entry.Rarity,
                    Grant = GrantCharacter(entry.CharacterId) });
                progress.Pulls++;
                if (progress.Pulls == 300)
                {
                    progress.Pulls = 0;
                    var entitlement = new LocalSelectorEntitlement { Id = requestId + ":selector:" + (receipt.SelectorsEarned + 1),
                        GroupId = _banner.GuaranteeGroupId, BannerRevision = _banner.Revision,
                        EligibleCharacterIds = _banner.FeaturedIds() };
                    _state.Selectors.Add(entitlement);
                    receipt.SelectorsEarned++;
                }
            }
            receipt.GuaranteeAfter = progress.Pulls;
            receipt.BalanceAfter = _state.Diamonds;
            _state.RandomState = _random.State.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _state.RandomDrawCount = (long)_random.DrawCount;
            _state.Revision++;
            _state.SummonReceipts.Add(receipt.Clone());
            return receipt;
        }

        public LocalSelectorEntitlement FindUnclaimedSelector()
        {
            return _state.Selectors.Find(x => x != null && !x.Claimed)?.Clone();
        }

        public CharacterGrantOutcome PreviewSelector(string entitlementId, string characterId)
        {
            var selector = FindSelector(entitlementId);
            if (selector == null || selector.Claimed) throw new InvalidOperationException("Featured selector is not available.");
            if (selector.EligibleCharacterIds == null || !selector.EligibleCharacterIds.Contains(characterId))
                throw new InvalidOperationException("Character is outside the selector's earned roster.");
            return PreviewGrant(characterId);
        }

        public CharacterGrantOutcome ClaimSelector(string entitlementId, string characterId, string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("A claim request ID is required.", nameof(requestId));
            var old = _state.ProgressionReceipts.Find(x => x != null && x.RequestId == requestId);
            if (old != null)
            {
                if (old.CharacterId != characterId || old.EntitlementId != entitlementId)
                    throw new InvalidOperationException("Request ID was already used for another claim.");
                if (old.Grant == null) throw new InvalidOperationException("Request ID belongs to a different progression action.");
                return old.Grant.Clone();
            }
            var selector = FindSelector(entitlementId);
            var outcome = PreviewSelector(entitlementId, characterId);
            ApplyGrant(outcome);
            selector.Claimed = true;
            selector.ClaimRequestId = requestId;
            selector.ClaimedCharacterId = characterId;
            _state.ProgressionReceipts.Add(new LocalEconomyProgressionReceipt { RequestId = requestId,
                CharacterId = characterId, EntitlementId = entitlementId, TierBefore = outcome.ConstellationTier, TierAfter = outcome.ConstellationTier,
                Grant = outcome.Clone() });
            _state.Revision++;
            return outcome;
        }

        public LocalEconomyProgressionReceipt UpgradeConstellation(string characterId, string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("An upgrade request ID is required.", nameof(requestId));
            var old = _state.ProgressionReceipts.Find(x => x != null && x.RequestId == requestId);
            if (old != null)
            {
                if (old.CharacterId != characterId || old.TierAfter != old.TierBefore + 1)
                    throw new InvalidOperationException("Request ID was already used for another progression action.");
                return old.Clone();
            }
            var owned = FindOwned(characterId);
            if (owned == null) throw new InvalidOperationException("Character is not owned.");
            if (owned.ConstellationTier >= 6) throw new InvalidOperationException("Character is already at C6.");
            if (owned.CrestCount < 1) throw new InvalidOperationException("One character-specific crest is required for the next tier.");
            var receipt = new LocalEconomyProgressionReceipt { RequestId = requestId, CharacterId = characterId,
                TierBefore = owned.ConstellationTier, TierAfter = owned.ConstellationTier + 1 };
            owned.CrestCount--;
            owned.ConstellationTier++;
            _state.ProgressionReceipts.Add(receipt.Clone());
            _state.Revision++;
            return receipt;
        }

        public void SaveFormation(IReadOnlyList<string> definitionIds)
        {
            if (definitionIds == null || definitionIds.Count != 4)
                throw new ArgumentException("Formation requires exactly four slots.", nameof(definitionIds));
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in definitionIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !unique.Add(id))
                    throw new InvalidOperationException("Formation entries must be four unique owned fighters.");
                if (FindOwned(id) == null) throw new InvalidOperationException(id + " is not owned by this profile.");
                var definition = _catalog.Characters.Find(item => item != null && item.Id == id && item.RuntimeReady);
                if (definition == null) throw new InvalidOperationException(id + " has no published runtime definition.");
            }
            _state.FormationDefinitionIds = new List<string>(definitionIds);
            _state.Revision++;
        }

        public LocalEconomyLedgerEntry GrantRunCompletion(string runId, int amount)
        {
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("A run ID is required.", nameof(runId));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var operationId = "run-completion:" + runId;
            var old = _state.Ledger.Find(x => x != null && x.OperationId == operationId);
            if (old != null)
            {
                if (old.Amount != amount) throw new InvalidOperationException("Run completion ID was reused with a different reward quote.");
                return old.Clone();
            }
            _state.Diamonds += amount;
            var entry = new LocalEconomyLedgerEntry { OperationId = operationId, Kind = "RunCompletion",
                Amount = amount, BalanceAfter = _state.Diamonds };
            _state.Ledger.Add(entry.Clone());
            _state.Revision++;
            return entry;
        }

        private BannerPoolEntry DrawEntry()
        {
            var roll = _random.NextBasisPoints();
            var cumulative = 0;
            foreach (var entry in _banner.Pool)
            {
                cumulative += entry.IndividualRateBp;
                if (roll < cumulative) return entry;
            }
            throw new InvalidOperationException("Banner rate table did not cover the complete 0-9999 roll range.");
        }

        private CharacterGrantOutcome GrantCharacter(string characterId)
        {
            var outcome = PreviewGrant(characterId);
            ApplyGrant(outcome);
            return outcome;
        }

        private CharacterGrantOutcome PreviewGrant(string characterId)
        {
            var owned = FindOwned(characterId);
            if (owned == null) return new CharacterGrantOutcome { CharacterId = characterId, Kind = CharacterGrantKind.NewCharacter,
                ConstellationTier = 0, CrestCount = 0, CollectionTokens = _state.CollectionTokens };
            if (owned.ConstellationTier >= 6) return new CharacterGrantOutcome { CharacterId = characterId,
                Kind = CharacterGrantKind.CollectionToken, ConstellationTier = owned.ConstellationTier,
                CrestCount = owned.CrestCount, CollectionTokens = _state.CollectionTokens + 1 };
            return new CharacterGrantOutcome { CharacterId = characterId, Kind = CharacterGrantKind.DuplicateCrest,
                ConstellationTier = owned.ConstellationTier, CrestCount = owned.CrestCount + 1,
                CollectionTokens = _state.CollectionTokens };
        }

        private void ApplyGrant(CharacterGrantOutcome outcome)
        {
            var owned = FindOwned(outcome.CharacterId);
            if (outcome.Kind == CharacterGrantKind.NewCharacter)
                _state.Roster.Add(new LocalOwnedCharacter { DefinitionId = outcome.CharacterId, ConstellationTier = 0 });
            else if (outcome.Kind == CharacterGrantKind.DuplicateCrest) owned.CrestCount++;
            else _state.CollectionTokens++;
        }

        private LocalOwnedCharacter FindOwned(string id) => _state.Roster.Find(x => x != null && x.DefinitionId == id);
        private LocalGuaranteeProgress FindProgress() => _state.GuaranteeProgress.Find(x => x != null && x.GroupId == _banner.GuaranteeGroupId);
        private LocalGuaranteeProgress FindOrCreateProgress()
        {
            var progress = FindProgress();
            if (progress != null) return progress;
            progress = new LocalGuaranteeProgress { GroupId = _banner.GuaranteeGroupId };
            _state.GuaranteeProgress.Add(progress);
            return progress;
        }
        private LocalSelectorEntitlement FindSelector(string id) => _state.Selectors.Find(x => x != null && x.Id == id);
    }
}
