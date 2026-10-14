// NetStock.cs
// Multiplayer: every station's shop stock (the items and the dealer's ships) is shared by all players. The host keeps one
// list per station, made with the single-player rules (Shop.GenerateItems / GenerateShips) the first time a player docks
// there, and made again every ResetSeconds (instead of the single-player re-roll after 3 other stations and the nibbling
// on re-docking); every player docked there gets it (NetState.StockRpc) and it replaces their own list in place
// (StationStock.items / ships; the bar's agents stay each player's). A trade is sent to the host (a bought or sold unit, a
// dealer row swapped), which applies it and sends the list to everyone docked there, so they see it at once (the hangar
// window rebuilds, StationMenu). Not shared: the owned Kaamo Club's storage (108) and a Stolen goods mission's documents
// (only its team sees them, Freelance.OnEnterStation; one member buying them takes them for the squad).
// The host decides: a unit it no longer has (another player bought the last one) is taken back from the buyer and paid
// back (ItemRefused); a dealer ship is reserved with the host before the trade (ReserveShip). Until the host's list is
// here the docked player's list is empty (no trade against a list of their own). The host's lists get the stations'
// docking extras (HostExtras: Kappa's EMP GL I at free play, energy cells at the deep science stations). Lists arriving
// are applied between frames (Flush), never in the middle of a trade. The host checks each trade (NetState: the trader is
// docked there, one unit of an item that exists, a dealer row only as reserved: the reserved one back or the trader's old
// hull), sends the list once per frame however many units changed, and caps a row (MaxRowAmount) and the dealer list
// (MaxShips), so no client grows the list everyone docked there receives.

using System;
using System.Collections.Generic;
using System.Text;
using GoF2Remake.Data;
using UnityEngine;

namespace GoF2Remake.Multiplayer
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    public static class NetStock
    {
        /// <summary>How long a station's stock lasts before the host makes it again.</summary>
        public const float ResetSeconds = 15f * 60f;

        class Entry
        {
            public List<ItemStack> items;
            public List<int> ships;
            public float made;
        }

        static readonly Dictionary<int, Entry> stocks = new Dictionary<int, Entry>();   // the host's
        static Database db;
        static readonly List<Action> pending = new List<Action>();
        static readonly Dictionary<long, (Action granted, Action refused)> reservations = new Dictionary<long, (Action, Action)>();
        static long Key(int station, int ship) => (long)station << 32 | (uint)ship;

        /// <summary>A station's stock changed here (another player's trade, the reset): its station.</summary>
        public static event Action<int> Changed;

        static bool Ready => NetGame.Active && NetState.Instance != null && NetState.Instance.IsSpawned;

        /// <summary>Is 'station's stock the shared one (in a session, not the owned Kaamo Club's storage)?</summary>
        public static bool Shared(int station) => NetGame.Active && !KaamoClub.StorageAt(station);

        /// <summary>A new session: the host's lists start empty.</summary>
        public static void Reset()
        {
            stocks.Clear();
            pending.Clear();
            reservations.Clear();
        }

        /// <summary>NetState, every frame: what arrived is applied now (not inside a trade).</summary>
        internal static void Flush()
        {
            if (pending.Count == 0) return;
            var now = pending.ToArray();
            pending.Clear();
            foreach (var a in now) a();
        }

        // ---- the players --------------------------------------------------------------------------------------

        /// <summary>Shop.EnterStation: ask the host for the shared stock (it replaces this list when it arrives).</summary>
        public static void OnEnterStation(StationStock stock)
        {
            if (!Ready || stock == null || !Shared(stock.station)) return;
            // Nothing to trade until the host's list is here (Apply); the bar's agents stay.
            stock.items.Clear();
            stock.ships?.Clear();   // (a traded-in row's mods wait for the list: Apply prunes against it)
            NetState.Instance.StockRequestRpc(stock.station);
        }

        /// <summary>Hangar.Buy / Sell: one unit of 'item' out (-1, bought for 'price') or in (+1).</summary>
        public static void ItemChanged(int station, int item, int delta, int price = 0)
        {
            if (!Ready || !Shared(station)) return;
            if (item == Freelance.Documents)
            {
                // The Stolen goods documents aren't stock: bought, they are gone for the squad (NetMissions.OnStatus).
                if (delta < 0 && Freelance.Active && Freelance.Mission.type == MissionType.StolenGoods && Freelance.Mission.status == 0)
                {
                    Freelance.Mission.status = 1;   // the buyer's own: no documents again in the next list either
                    NetMissions.AddStatus(Freelance.Mission, 1);
                }
                return;
            }
            NetState.Instance.StockItemRpc(station, item, delta, price);
        }

        /// <summary>NetState: the host had no unit left of 'count' units this player bought at 'price' each (someone was first):
        /// back they go, paid back.</summary>
        internal static void ItemRefused(int station, int item, int price, int count = 1) => pending.Add(() =>
        {
            int back = 0;
            for (int n = 0; n < count; n++)
            {
                if (Story.CargoOf(item) > 0) Shop.RemoveFromCargo(item, 1);
                else
                {
                    var mounted = Session.Equipment.Find(e => e.item == item);
                    if (mounted == null) break;   // already sold on: nothing to take back
                    if (mounted.amount > 1) mounted.amount--; else Session.Equipment.Remove(mounted);
                }
                back++;
            }
            if (back == 0) return;
            Session.Credits += price * back;
            NetChat.Notice(string.Format(Localization.Extra("mpSoldOut", "Sold out: another pilot bought the last {0}."), UI.ItemInfo.ItemName(item)));
            Changed?.Invoke(station);
        });

        /// <summary>Hangar's ship trade: the dealer's row 'ship' is reserved with the host first (another pilot may be buying
        /// it); 'granted' trades, 'refused' tells. Outside a session: at once.</summary>
        public static void ReserveShip(int station, int ship, Action granted, Action refused)
        {
            if (!Ready || !Shared(station)) { granted(); return; }
            reservations[Key(station, ship)] = (granted, refused);
            NetState.Instance.ReserveShipRpc(station, ship);
        }

        /// <summary>NetState: the reservation's answer.</summary>
        internal static void ReserveResult(int station, int ship, bool ok) => pending.Add(() =>
        {
            if (!reservations.TryGetValue(Key(station, ship), out var r)) { if (ok) ShipChanged(station, -1, ship); return; }   // not wanted any more: back
            reservations.Remove(Key(station, ship));
            if (ok) r.granted(); else r.refused();
        });

        /// <summary>Hangar.BuyShip / KeepAndBuyShip: the dealer's row 'removed' became 'added' (-1 = none).</summary>
        public static void ShipChanged(int station, int removed, int added)
        {
            if (!Ready || !Shared(station)) return;
            NetState.Instance.StockShipRpc(station, removed, added);
        }

        /// <summary>NetState: the host's stock of 'station' (Encode): into this player's list for it (between frames).</summary>
        internal static void Apply(int station, string text) => pending.Add(() => ApplyNow(station, text));

        static void ApplyNow(int station, string text)
        {
            var stock = Session.RecentStations.Find(s => s.station == station);
            if (stock == null || !Shared(station)) return;
            Decode(text, out var items, out var ships);
            stock.items.Clear();
            stock.items.AddRange(items);
            if (stock.ships == null) stock.ships = new List<int>();
            stock.ships.Clear();
            stock.ships.AddRange(ships);
            stock.PruneShipMods();   // the shared list carries no mods: a modded row only this player traded in keeps them
            Freelance.OnEnterStation(stock);   // a Stolen goods mission's documents (only its team's)
            Changed?.Invoke(station);
        }

        /// <summary>NetMissions: a squadmate bought the documents: gone from this player's list too.</summary>
        internal static void RemoveDocuments(int station)
        {
            var stock = Session.RecentStations.Find(s => s.station == station);
            if (stock == null || stock.items.RemoveAll(r => r.item == Freelance.Documents) == 0) return;
            Changed?.Invoke(station);
        }

        // ---- the host -----------------------------------------------------------------------------------------

        static Entry Get(int station)
        {
            if (stocks.TryGetValue(station, out var e)) return e;
            if (db == null) db = Database.Load();
            e = new Entry { items = Shop.GenerateItems(db, station), ships = Shop.GenerateShips(db, station) ?? new List<int>(), made = Time.unscaledTime };
            HostExtras(station, e.items);
            stocks[station] = e;
            return e;
        }

        /// <summary>Story.OnDocked's stock extras that aren't about one player, in the shared list (a player's own copy would be
        /// replaced by it): the deep science stations' and the battlestation's energy cells (the single-player rule gives them for a nearly empty hold: here always, so
        /// nobody is stranded in a gateless system).</summary>
        static void HostExtras(int station, List<ItemStack> items)
        {
            void Add(int item, int amount)
            {
                var row = items.Find(r => r.item == item);
                if (row != null) { row.amount += amount; return; }
                int at = items.FindIndex(r => r.item > item);
                items.Insert(at < 0 ? items.Count : at, new ItemStack(item, amount));
            }
            if ((station == 10 || station == 100 || station == 101) && !items.Exists(r => r.item == GalaxyMap.EnergyCellItem)) Add(GalaxyMap.EnergyCellItem, 10);
        }

        /// <summary>A dealer ship's reservation: taken from the host's list (false = none left: another pilot has it).</summary>
        internal static bool HostReserveShip(int station, int ship)
        {
            var e = Get(station);
            int row = e.ships.IndexOf(ship);
            if (row < 0) return false;
            e.ships.RemoveAt(row);
            return true;
        }

        /// <summary>The stock of 'station' (made now if there is none), encoded for the RPC.</summary>
        internal static string HostGet(int station) => Encode(Get(station));

        /// <summary>One unit of 'item' bought (-1) or sold (+1) at 'station'; false = refused (no unit left to buy). (The
        /// clients only send what their own game shares: no Kaamo check here, the host's club isn't theirs.)</summary>
        /// <summary>The host's stock row changes by 'delta' units (a sale +, a purchase -; several at once from a shop's
        /// Buy all / Sell all, Hangar.EndBatch). Returns the units done: a purchase gets only what the row still has (another
        /// pilot may have bought some first); the caller refuses the rest.</summary>
        internal static int HostItem(int station, int item, int delta)
        {
            if (delta == 0) return 0;
            var e = Get(station);
            var row = e.items.Find(r => r.item == item);
            if (delta < 0)
            {
                if (row == null || row.amount <= 0) return 0;   // another pilot bought the last one
                int granted = Math.Min(row.amount, -delta);
                row.amount -= granted;
                if (row.amount <= 0) e.items.Remove(row);
                return granted;
            }
            else if (row != null) row.amount = Math.Min(row.amount + delta, MaxRowAmount);   // a seller can't grow a row without end
            else
            {
                int at = e.items.FindIndex(r => r.item > item);
                e.items.Insert(at < 0 ? e.items.Count : at, new ItemStack(item, Math.Min(delta, MaxRowAmount)));   // item index order, like Shop
            }
            return delta;
        }

        /// <summary>A dealer trade at 'station': the row 'removed' becomes 'added' (in place), or goes / comes.</summary>
        internal static bool HostShip(int station, int removed, int added)
        {
            var e = Get(station);
            int row = removed >= 0 ? e.ships.IndexOf(removed) : -1;
            if (row >= 0 && added >= 0) e.ships[row] = added;
            else if (row >= 0) e.ships.RemoveAt(row);
            else if (added >= 0 && e.ships.Count < MaxShips) e.ships.Add(added);
            return true;
        }

        /// <summary>The most units of one item and the most dealer ships the host keeps for a station (the generator's are
        /// far fewer; trades beyond them don't grow the list sent to everyone docked there).</summary>
        const int MaxRowAmount = 100000, MaxShips = 32;

        /// <summary>NetState's sweep: every stock older than ResetSeconds is made again and sent to the players docked there.</summary>
        internal static void HostTick(NetState state)
        {
            if (stocks.Count == 0) return;
            List<int> expired = null;
            foreach (var kv in stocks)
                if (Time.unscaledTime - kv.Value.made >= ResetSeconds) (expired ??= new List<int>()).Add(kv.Key);
            if (expired == null) return;
            foreach (int station in expired)
            {
                stocks.Remove(station);
                Get(station);
                state.BroadcastStock(station);
            }
        }

        // ---- the wire format: "item:amount,...|ship,..." --------------------------------------------------------

        static string Encode(Entry e)
        {
            var sb = new StringBuilder();
            foreach (var r in e.items)
            {
                if (r.amount <= 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(r.item).Append(':').Append(r.amount);
            }
            sb.Append('|');
            for (int i = 0; i < e.ships.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(e.ships[i]);
            }
            return sb.ToString();
        }

        static void Decode(string text, out List<ItemStack> items, out List<int> ships)
        {
            items = new List<ItemStack>();
            ships = new List<int>();
            if (string.IsNullOrEmpty(text)) return;
            var parts = text.Split('|');
            foreach (var one in parts[0].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = one.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[0], out int item) && int.TryParse(kv[1], out int amount)) items.Add(new ItemStack(item, amount));
            }
            if (parts.Length < 2) return;
            foreach (var one in parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(one, out int ship)) ships.Add(ship);
        }
    }
}
