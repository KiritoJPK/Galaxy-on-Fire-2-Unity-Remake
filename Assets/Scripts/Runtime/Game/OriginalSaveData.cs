// OriginalSaveData.cs
// The original game's save record (Galaxy on Fire 2 HD for Android, RecordHandler::recordStoreWrite 0xdf760 /
// recordStoreRead): every field in the order the game writes it, so a record read and written again is byte for byte the
// same. Field names follow the remake's knowledge of Status (CLAUDE.md); a field whose meaning isn't known yet is named by
// its Status offset. Read and written by OriginalSave; mapping onto the remake's SaveData comes later.

using System.Collections.Generic;

namespace GoF2Remake.Data
{
    /// <summary>A save slot of the original game (gof2_save_game_N): the GameRecord fields in file order.</summary>
    public class OriginalRecord
    {
        public bool[] visitedStations = new bool[0];          // Galaxy::getVisited (135)
        public int credits, rating;
        public long playingTimeMs;
        public int kills, missionCount, level, lastXp, goodsProduced, stationsVisited, campaignMission;
        public OriginalMission freelanceMission = new OriginalMission(), storyMission = new OriginalMission();
        public int jumpgatesUsed, capturedCrates, boughtEquipment, pirateKills;
        public int status80, status7c, voidReturnStation, status88;   // Status+0x80 / +0x7c (the Void invasion), +0x84, +0x88
        public bool[] oreTypesMined = new bool[0], coreTypesMined = new bool[0];   // Status+0x94 / +0x98
        public int courierGoods, oresMined, coresMined, boozeBought;                  // Status+0x9c .. +0xa8
        public bool[] boozeTypes = new bool[0];                                      // Status+0xac
        public int spaceJunk;                                                        // Status+0xb0
        public bool[] statusB4 = new bool[0];                                        // Status+0xb4
        public int passengersCarried;                                                // Status+0xb8
        public long cloakMs;                                                         // Status+0xc0
        public int[] statusC8 = new int[10];                                         // Status+0xc8 .. +0xec, 10 counters
        public int[] medals = new int[0];                                            // Achievements::getMedals (45)
        public int ship, shipRace;
        public List<OriginalEquipment> equipment = new List<OriginalEquipment>();
        public List<OriginalItem> cargo = new List<OriginalItem>();
        /// <summary>Status::getStationStack: the 3 remembered stations, then the current one (Status::getStation); null = none.</summary>
        public OriginalStation[] stations = new OriginalStation[4];
        public int[] standings = new int[2];
        public List<OriginalBlueprint> blueprints = new List<OriginalBlueprint>();
        public List<OriginalPendingProduct> pendingProducts = new List<OriginalPendingProduct>();
        /// <summary>Status+0x24 (wingmen names) with +0x2c, +0x30 and the 5 ints of +0x28; null = none (written as -1).</summary>
        public OriginalWingmen wingmen;
        public int passengers;                                                       // Status+0x34
        public bool[] systemsVisible = new bool[0];                                  // Status+0x38
        public int[] status40 = new int[0], status3c = new int[0], status48 = new int[0], status44 = new int[0];
        public bool[] status4c = new bool[0];                                        // Status+0x4c
        public List<OriginalAgent> agents = new List<OriginalAgent>();               // Status::getAgents
        /// <summary>Globals::hints, by hint index (only the ones the record holds are written).</summary>
        public bool[] hints = new bool[0x3b];
        public float difficulty;                                                     // options+0x2c
        public long status100;                                                       // Status+0x100
        /// <summary>Status+0x8c: the ship parked while a loaner is flown; null = none.</summary>
        public OriginalParkedShip parkedShip;
        public int[] storyTargets = new int[0];                                      // Status+0x90
        public int status10c;
        public bool status110;
        public int kaamoState;                                                       // Status+0x114
        public bool status111;
        public List<OriginalItem> kaamoItems = new List<OriginalItem>();             // Status+0x14c's stock
        public List<OriginalShip> kaamoShips = new List<OriginalShip>();
        public bool options35, options36;
        public bool[] itemsObtained = new bool[0];                                   // Status+0x54

        // ---- after the 0x6e6a78 marker (later versions; a record without it ends above) ----
        public bool extended;
        public int[] shipMods = new int[0];
        public int[] parkedShipMods = new int[0];
        public List<OriginalWanted> wanted = new List<OriginalWanted>();
        public int[] collectedBounties = new int[4];
        public bool options37;
        public int status174;
        public bool[] status58 = new bool[0];
        public int status118;
        /// <summary>Bytes after the last known field (none in a record the game wrote); kept so it is written back as read.</summary>
        public byte[] trailing = new byte[0];
    }

    public class OriginalItem
    {
        public int item, amount, price;
        public bool unsaleable;
    }

    /// <summary>An equipment slot: item -1 = empty.</summary>
    public class OriginalEquipment
    {
        public int item = -1, amount;
        public bool unsaleable;
    }

    public class OriginalShip
    {
        public int ship, race;
        public int[] mods = new int[0];   // Ship::getMods: the stored and dealer hulls' Kaamo mods (after the marker)
    }

    public class OriginalStation
    {
        public int index;
        public List<OriginalItem> items = new List<OriginalItem>();
        public List<OriginalShip> ships = new List<OriginalShip>();
        public List<OriginalAgent> agents = new List<OriginalAgent>();
        public bool attackedFriends;
    }

    /// <summary>BluePrint: the invested amount per ingredient (the count is the product's recipe, not stored), then +4, the
    /// unlocked flag, +0xc, the production station (-1 none) and its name.</summary>
    public class OriginalBlueprint
    {
        public int[] invested = new int[0];
        public int field4;
        public bool unlocked;
        public int fieldC, station = -1;
        public string stationName = "";
    }

    /// <summary>PendingProduct, in file order (PendingProduct+0x10, +0xc, +8, then its name string).</summary>
    public class OriginalPendingProduct
    {
        public int a, b, c;
        public string name = "";
    }

    public class OriginalWingmen
    {
        public List<string> names = new List<string>();
        public int status2c, status30;
        public int[] status28 = new int[5];
    }

    public class OriginalParkedShip
    {
        public int ship, race;
        public List<OriginalEquipment> equipment = new List<OriginalEquipment>();
        public List<OriginalItem> cargo = new List<OriginalItem>();
    }

    /// <summary>Mission (writeMission): type -1 = empty, nothing else stored.</summary>
    public class OriginalMission
    {
        public int type = -1;
        public string clientName = "", targetName = "", targetStationName = "", targetSystemName = "";
        public bool campaign;
        public int[] clientImage;   // null = none (-1), else 5 parts
        public int clientRace, costs, bonus, reward, targetStation, difficulty, goodIndex, goodAmount, status;
        public bool visible;
        public OriginalAgent agent;   // null = none
        public bool Empty => type == -1;
    }

    /// <summary>Agent (writeAgent); its mission is null when none, or when it is the mission being written (no loop).</summary>
    public class OriginalAgent
    {
        public int costs, sellSystem, sellBlueprint, eventState, index, offer, race, sellItem, sellItemPrice, sellItemAmount,
                   station, system, wingmanFriends;
        public bool male, hasReward, acceptedOffer, flag1c, flag1d;
        public int[] imageParts;   // null = none (-1), else 5
        public int sellMod;        // only stored for an index above 0x12
        public string missionText = "", name = "", stationName = "", systemName = "", text8 = "", textC = "";
        public OriginalMission mission;
    }

    public class OriginalWanted
    {
        public bool active, terminated;
        public int location, travelsTo, lastSeen;
        public string name = "";
        public int index, board, race;
        public bool male;
        public int ship, weapon, hitpoints, loot, lootAmount, reward, requiredBounties, requiredMission, wingmen;
        public int[] imageParts = new int[5];
    }

    /// <summary>The slot list's preview (gof2_save_game_preview_N, recordStoreWritePreview): not signed.</summary>
    public class OriginalPreview
    {
        public long playingTimeMs;
        public int credits;
        public string station = "", system = "";
        public int campaignMission, level;
        public float difficulty;
        public int ship;
    }
}
