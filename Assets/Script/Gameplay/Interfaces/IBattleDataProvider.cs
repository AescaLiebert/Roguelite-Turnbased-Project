using System.Collections.Generic;

public interface IBattleDataProvider
{
    List<TransferTeamData> GetLocalPlayerTeam();
    List<TransferTeamData> GetOpponentTeam();
}
