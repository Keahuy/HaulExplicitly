using Verse;
using Verse.AI;
using RimWorld;
using Vector3 = UnityEngine.Vector3;
using System.Reflection;
using HarmonyLib;


namespace HaulExplicitly
{
    public class HaulExplicitlyInventoryRecord : IExposable
    {
        //data
        private System.WeakReference _parentPosting;

        public HaulExplicitlyPosting ParentPosting
        {
            get
            {
                if (_parentPosting != null) return (HaulExplicitlyPosting)_parentPosting.Target;// 弱引用，Save and Load 存档后会被清理掉，所以要判断是否为空
                foreach (var posting in from mgr in HaulExplicitly.GetManagers() from posting in mgr.Postings.Values where posting.Inventory.Contains(this) select posting)
                {
                    return (HaulExplicitlyPosting)(_parentPosting = new WeakReference(posting)).Target;
                }
                return (HaulExplicitlyPosting)_parentPosting.Target;
            }
        }

        public List<Thing> Items = new List<Thing>();
        private ThingDef _itemDef;

        public ThingDef ItemDef
        {
            get => _itemDef;
            private set => _itemDef = value;
        }

        private ThingDef _itemStuff;

        public ThingDef ItemStuff
        {
            get => _itemStuff;
            private set => _itemStuff = value;
        }

        private ThingDef _miniDef;

        public ThingDef MiniDef
        {
            get => _miniDef;
            private set => _miniDef = value;
        }

        private int _selectedQuantity;

        public int SelectedQuantity
        {
            get => _selectedQuantity;
            private set => _selectedQuantity = value;
        }

        private int _playerSetQuantity = -1;

        public int SetQuantity
        {
            get => (_playerSetQuantity == -1) ? SelectedQuantity : _playerSetQuantity;
            set
            {
                if (value < 0 || value > SelectedQuantity)
                    throw new ArgumentOutOfRangeException();
                _playerSetQuantity = (int)value;
            }
        }

        public bool PlayerChangedQuantity => _playerSetQuantity != -1;

        private int _mergeCapacity;

        public int MergeCapacity
        {
            get => _mergeCapacity;
            private set => _mergeCapacity = value;
        }

        private int _numMergeStacksWillUse;

        public int NumMergeStacksWillUse
        {
            get => _numMergeStacksWillUse;
            private set => _numMergeStacksWillUse = value;
        }

        public int MovedQuantity = 0;

        //
        public void ExposeData()
        {
            Scribe_Collections.Look(ref Items, "items", LookMode.Reference);
            Scribe_Defs.Look(ref _itemDef, "itemDef");
            Scribe_Defs.Look(ref _itemStuff, "itemStuff");
            Scribe_Defs.Look(ref _miniDef, "minifiableDef");
            Scribe_Values.Look(ref _selectedQuantity, "selectedQuantity");
            Scribe_Values.Look(ref _playerSetQuantity, "setQuantity");
            Scribe_Values.Look(ref _mergeCapacity, "mergeCapacity");
            Scribe_Values.Look(ref _numMergeStacksWillUse, "numMergeStacksWillUse");
            Scribe_Values.Look(ref MovedQuantity, "movedQuantity");
        }

        //methods

        public HaulExplicitlyInventoryRecord()
        {
            // 防报错：SaveableFromNode exception: System.MissingMethodException: Constructor on type 'HaulExplicitly.HaulExplicitlyInventoryRecord' not found.   
        }

        public HaulExplicitlyInventoryRecord(Thing initial, HaulExplicitlyPosting parentPosting)
        {
            _parentPosting = new System.WeakReference(parentPosting);
            Items.Add(initial);
            ItemDef = initial.def;
            ItemStuff = initial.Stuff;
            MiniDef = (initial as MinifiedThing)?.InnerThing.def;
            SelectedQuantity = initial.stackCount;
            ResetMerge();
        }

        public void ResetMerge()
        {
            MergeCapacity = 0;
            NumMergeStacksWillUse = 0;
        }

        public void AddMergeCell(int itemQuantity)
        {
            NumMergeStacksWillUse++;
            MergeCapacity += ItemDef.stackLimit - itemQuantity;
        }

        public static int StacksWorth(ThingDef td, int quantity)
        {
            return (quantity / td.stackLimit) + ((quantity % td.stackLimit == 0) ? 0 : 1);
        }

        public int NumStacksWillUse => StacksWorth(ItemDef, Math.Max(0, SetQuantity - MergeCapacity)) + NumMergeStacksWillUse;

        public bool CanMixWith(Thing t)
        {
            return (t.def.category == ThingCategory.Item
                    && ItemDef == t.def
                    && ItemStuff == t.Stuff
                    && MiniDef == (t as MinifiedThing)?.InnerThing.def);
        }

        public bool HasItem(Thing t)
        {
            return Items.Contains(t);
        }

        public bool TryAddItem(Thing t, bool sideEffects = true)
        {
            if (!CanMixWith(t))
                return false;
            Items.Add(t);
            if (sideEffects)
                SelectedQuantity += t.stackCount;
            return true;
        }

        public bool TryRemoveItem(Thing t, bool playerCancelled = false)
        {
            bool r = Items.Remove(t);
            if (r && playerCancelled)
            {
                SelectedQuantity -= t.stackCount;
                _playerSetQuantity = Math.Min(_playerSetQuantity, SelectedQuantity);
            }

            return r;
        }

        public int RemainingToHaul()
        {
            var pawnsList = new List<Pawn>(ParentPosting.Map.mapPawns.PawnsInFaction(Faction.OfPlayer));
            int beingHauledNow = 0;
            foreach (Pawn p in pawnsList)
            {
                if (p.jobs.curJob == null)
                {
                    continue;
                }

                if (p.jobs.curJob.def.driverClass == typeof(JobDriver_HaulExplicitly) && this == ((JobDriver_HaulExplicitly)p.jobs.curDriver).record)
                {
                    beingHauledNow += p.jobs.curJob.count;
                }
            }

            return Math.Max(0, SetQuantity - (MovedQuantity + beingHauledNow));
        }

        public string Label
        {
            get { return GenLabel.ThingLabel(MiniDef ?? ItemDef, ItemStuff, SetQuantity).CapitalizeFirst(); }
        }
    }

    [HarmonyPatch(typeof(CompressibilityDecider), "DetermineReferences")]
    class CompressibilityDecider_DetermineReferences_Patch
    {
        static void Postfix(CompressibilityDecider __instance)
        {
            HashSet<Thing> referencedThings =
                (HashSet<Thing>)typeof(CompressibilityDecider).InvokeMember("referencedThings",
                    BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic,
                    null, __instance, null);
            Map map = (Map)typeof(CompressibilityDecider).InvokeMember("map",
                BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic,
                null, __instance, null);

            foreach (Thing t in HaulExplicitly.GetManager(map).Haulables)
                referencedThings.Add(t);
        }
    }

    public enum HaulExplicitlyStatus : byte
    {
        Planning, //hasn't been posted yet
        InProgress,
        DestinationBlocked, //one or more item types can't fit in their destinations right now
        Incompletable, //all possible hauls have been done (inventory exhausted), but the job is incomplete
        Complete,
        OverkillError //one of the records has had too much hauled
    }

    public class HaulExplicitlyPosting : IExposable
    {
        public HaulExplicitlyPosting()
        {
        }

        private int _id;
        private Map? _map;

        public int ID
        {
            get => _id;
            private set => _id = value;
        }

        public Map Map
        {
            get => _map;
            private set => _map = value;
        }

        public List<HaulExplicitlyInventoryRecord> Inventory = [];
        public List<Thing> Items = [];
        public List<IntVec3>? Destinations = null;

        public Vector3? Cursor = null;
        public Vector3 Center = new Vector3();
        public float VisualizationRadius = 0.0f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref _id, "postingId");
            Scribe_References.Look(ref _map, "map", true);
            Scribe_Collections.Look(ref Inventory, "inventory", LookMode.Deep);
            Scribe_Collections.Look(ref Destinations, "destinations", LookMode.Value);
            Scribe_Values.Look(ref Cursor, "cursor");
            Scribe_Values.Look(ref Center, "center");
            Scribe_Values.Look(ref VisualizationRadius, "visualizationRadius");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ReloadItemsFromInventory();
            }
        }

        public HaulExplicitlyPosting(IEnumerable<object> objects)
        {
            ID = HaulExplicitly.GetNewPostingID();
            Map = Find.CurrentMap;
            foreach (object o in objects)
            {
                Thing t = o as Thing;
                if (t == null || !t.def.EverHaulable)
                {
                    continue;
                }

                Items.Add(t);
                foreach (HaulExplicitlyInventoryRecord record in Inventory)
                {
                    if (record.TryAddItem(t))
                    {
                        goto match;
                    }
                }

                Inventory.Add(new HaulExplicitlyInventoryRecord(t, this));
                match:
                {
                }
            }
        }

        public bool TryRemoveItem(Thing t, bool playerCancelled = false)
        {
            if (!Items.Contains(t))
                return false;
            HaulExplicitlyInventoryRecord? ownerRecord = null;
            foreach (var record in Inventory)
            {
                if (record.HasItem(t))
                {
                    ownerRecord = record;
                    break;
                }
            }

            if (ownerRecord == null || !ownerRecord.TryRemoveItem(t, playerCancelled))
            {
                Log.Error("Something went wronghbhnoetb9ugob9g3b49.");
                return false;
            }

            Items.Remove(t);
            return true;
        }

        public bool TryAddItemSplinter(Thing t)
        {
            if (Items.Contains(t))
                return false;
            var recordFinder = Inventory.GetEnumerator();
            while (recordFinder.MoveNext())
                if (recordFinder.Current.CanMixWith(t))
                    goto found;
            Log.Error("TryAddItemSplinter failed to find matching record for " + t);
            return false;
            found:
            Items.Add(t);
            recordFinder.Current.TryAddItem(t, false);
            return true;
        }

        public HaulExplicitlyInventoryRecord RecordWithItem(Thing t)
        {
            return Enumerable.FirstOrDefault(Inventory, record => record.HasItem(t));
        }

        public void Clean()
        {
            var destroyedItems = new List<Thing>(Items.Where(i => i.Destroyed));
            foreach (var i in destroyedItems)
            {
                TryRemoveItem(i);
            }
        }

        public void ReloadItemsFromInventory()
        {
            Items = new List<Thing>();
            foreach (var t in Inventory.SelectMany(r => r.Items))
            {
                Items.Add(t);
            }
        }

        private void InventoryResetMerge()
        {
            foreach (HaulExplicitlyInventoryRecord record in Inventory)
            {
                record.ResetMerge();
            }
        }

        public HaulExplicitlyStatus Status()
        {
            throw new NotImplementedException();
        }

        private bool IsPossibleItemDestination(IntVec3 c)
        {
            if (!c.InBounds(this.Map)
                || c.Fogged(this.Map)
                || c.InNoZoneEdgeArea(this.Map)
                || c.GetTerrain(this.Map).passability == Traversability.Impassable
               )
                return false;
            return this.Map.thingGrid.ThingsAt(c).All(t => t.def.CanOverlapZones && t.def.passability != Traversability.Impassable && !t.def.IsDoor);
        }

        private IEnumerable<IntVec3> PossibleItemDestinationsAtCursor(Vector3 cursor)
        {
            IntVec3 cursorCell = new IntVec3(cursor);
            var cardinals = new IntVec3[]
            {
                IntVec3.North, IntVec3.South, IntVec3.East, IntVec3.West
            };
            HashSet<IntVec3> expended = [];
            HashSet<IntVec3> available = [];
            if (IsPossibleItemDestination(cursorCell))
                available.Add(cursorCell);
            while (available.Count > 0)
            {
                IntVec3 nearest = new IntVec3();
                float nearestDist = 100000000.0f;
                foreach (IntVec3 c in available)
                {
                    float dist = (c.ToVector3Shifted() - cursor).magnitude;
                    if (!(dist < nearestDist)) continue;
                    nearest = c;
                    nearestDist = dist;
                }

                yield return nearest;
                available.Remove(nearest);
                expended.Add(nearest);

                foreach (IntVec3 dir in cardinals)
                {
                    IntVec3 c = nearest + dir;
                    if (expended.Contains(c) || available.Contains(c)) continue;
                    var set = IsPossibleItemDestination(c) ? available : expended;
                    set.Add(c);
                }
            }
        }

        public bool TryMakeDestinations(Vector3 cursor, bool tryBeLazy = true)
        {
            if (tryBeLazy && cursor == this.Cursor)
            {
                return Destinations != null;
            }

            this.Cursor = cursor;
            int minStacks = Inventory.Sum(record => record.NumStacksWillUse);

            InventoryResetMerge();
            var destinations = new List<IntVec3>();
            var prospects = PossibleItemDestinationsAtCursor(cursor).GetEnumerator();
            while (prospects.MoveNext())
            {
                IntVec3 cell = prospects.Current;
                List<Thing> itemsInCell = GetItemsIfValidItemSpot(Map, cell);
                if (Map.reservationManager.IsReservedByAnyoneOf(cell, Faction.OfPlayer)
                    || itemsInCell == null)
                    continue;

                if (itemsInCell.Count == 0)
                {
                    destinations.Add(cell);
                }
                else
                {
                    Thing item = itemsInCell.First();
                    if (itemsInCell.Count != 1 || Items.Contains(item))
                        continue;
                    //probably not necessary-- commented out for future reference:
                    //if (map.reservationManager.IsReservedByAnyoneOf(i, Faction.OfPlayer))
                    //    continue;

                    foreach (var record in Inventory.Where(record => record.CanMixWith(item) && item.stackCount != item.def.stackLimit))
                    {
                        destinations.Add(cell);
                        record.AddMergeCell(item.stackCount);
                        break;
                    }
                }

                if (destinations.Count < minStacks) continue; //this check is just so it doesn't do the more expensive check every time
                {
                    int stacks = Inventory.Sum(record => record.NumStacksWillUse);
                    if (destinations.Count < stacks) continue;
                    //success operations
                    Vector3 sum = destinations.Aggregate(Vector3.zero, (current, dest) => current + dest.ToVector3Shifted());
                    Center = (1.0f / (float)destinations.Count) * sum;
                    VisualizationRadius = (float)Math.Sqrt(destinations.Count / Math.PI);
                    Destinations = destinations;
                    return true;
                }
            }

            Destinations = null;
            return false;
        }

        public static List<Thing> GetItemsIfValidItemSpot(Map map, IntVec3 cell)
        {
            //references used for this function (referenced during Rimworld 0.19):
            // Designation_ZoneAddStockpile.CanDesignateCell
            // StoreUtility.IsGoodStoreCell
            var result = new List<Thing>();
            if (!cell.InBounds(map)
                || cell.Fogged(map)
                || cell.InNoZoneEdgeArea(map)
                || cell.GetTerrain(map).passability == Traversability.Impassable
                || cell.ContainsStaticFire(map))
                return null;
            List<Thing> things = map.thingGrid.ThingsListAt(cell);
            foreach (Thing thing in things)
            {
                if (!thing.def.CanOverlapZones
                    || (thing.def.entityDefToBuild != null
                        && thing.def.entityDefToBuild.passability != Traversability.Standable)
                    || (thing.def.surfaceType == SurfaceType.None
                        && thing.def.passability != Traversability.Standable))
                    return null;
                if (thing.def.EverStorable(false))
                    result.Add(thing);
            }

            return result;
        }

        internal string stringy_details()
        {
            //this function will output a bunch of the inner variables into a string
            var s = new List<string>([
                "HaulExplicitlyPosting #",
                ID + "\n",
                "total inventory records: " + Inventory.Count + "\n",
                "map = " + Map.ToString() + "\n"
            ]);
            var inventoryAllItems = new List<Thing>();
            var inventoryReadout = new List<string>(["Inventory readout:\n"]);
            for (int i = 0; i < Inventory.Count; i++)
            {
                inventoryAllItems = new List<Thing>(inventoryAllItems.Concat(Inventory[i].Items));
                inventoryReadout.Add("  (inventory record " + i + "[" + Inventory[i].ItemDef.defName + "])\n      ");
                inventoryReadout.AddRange(Inventory[i].Items.Select(item => " (" + item + ")"));
            }

            string coherent = "-";
            try
            {
                var a = new List<string>(Items.Select(i => i.ThingID));
                var b = new List<string>(inventoryAllItems.Select(i => i.ThingID));
                a.Sort();
                b.Sort();
                //Log.Message(string.Join(" ", a.ToArray()));
                //Log.Message(string.Join(" ", b.ToArray()));
                coherent = a.SequenceEqual(b).ToString();
                //*if (_same_length_hash(new List<string>(items.Select(i => i.ThingID)))
                //    .SetEquals(_same_length_hash(new List<string>(inventory_all_items.Select(i => i.ThingID)))))
                //    coherent = "true";
                //else
                //    coherent = "false";*/
            }
            catch
            {
                coherent = "false (exception)";
            }

            s.Add("Coherent: " + coherent + "\n");
            //s.Add("items:\n");
            //foreach (var i in items)
            //    s.Add("   (" + i + ")");
            //s.Add("\n");
            s = new List<string>(s.Concat(inventoryReadout));
            s.Add("\ndestinations:\n");
            s.AddRange(from r in Inventory from dest in Destinations select "  (" + dest + ")");
            s.Add("\n");
            //s.Add("inventory:\n");
            //foreach (var r in inventory)
            //    s.Add("")
            return string.Join("", s.ToArray());
        }
    }

    public class HaulExplicitlyJobManager : IExposable
    {
        private Map _map;

        public Map Map
        {
            get => _map;
            private set => _map = value;
        }

        public Dictionary<int, HaulExplicitlyPosting> Postings;

        public IEnumerable<Thing> Haulables
        {
            get { return Postings.Values.SelectMany(posting => posting.Items); }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref _map, "map", true);
            Scribe_Collections.Look(ref Postings, "postings", LookMode.Value, LookMode.Deep);
        }

        public void CleanGarbage()
        {
            var keys = new List<int>(Postings.Keys);
            foreach (int k in keys)
            {
                Postings[k].Clean();
                //var status = postings[k].Status();
                //if (status == HaulExplicitlyJobStatus.Complete
                //    || status == HaulExplicitlyJobStatus.Incompletable)
                //    postings.Remove(k);
            }
        }

        public HaulExplicitlyJobManager()
        {
            Postings = new Dictionary<int, HaulExplicitlyPosting>();
        }

        public HaulExplicitlyJobManager(Map map)
        {
            this.Map = map;
            Postings = new Dictionary<int, HaulExplicitlyPosting>();
        }

        public HaulExplicitlyPosting? PostingWithItem(Thing item)
        {
            return Postings.Values.FirstOrDefault(posting => posting.Items.Contains(item));
        }
    }

    public class DeliverableDestinations
    {
        public List<IntVec3> PartialCells = [];
        public List<IntVec3> FreeCells = [];
        private Func<IntVec3, float> _grader;
        public HaulExplicitlyPosting Posting { get; private set; }
        public HaulExplicitlyInventoryRecord Record { get; private set; }
        private int _destsWithThisStackType = 0;
        public List<int> PartialCellSpaceAvailable = [];
        private Thing _thing;

        private DeliverableDestinations(Thing item, Pawn carrier, HaulExplicitlyPosting posting, Func<IntVec3, float> grader)
        {
            this._grader = grader;
            this.Posting = posting;
            Record = posting.RecordWithItem(item);
            Map map = posting.Map;
            _thing = item;
            IntVec3 itemPos = (!item.SpawnedOrAnyParentSpawned) ? carrier.PositionHeld : item.PositionHeld;
            var traverseparms = TraverseParms.For(carrier, Danger.Deadly, TraverseMode.ByPawn, false);
            foreach (IntVec3 cell in posting.Destinations)
            {
                List<Thing> itemsInCell = HaulExplicitlyPosting.GetItemsIfValidItemSpot(map, cell);
                bool validDestination = itemsInCell != null;

                //see if this cell already has, or will have, an item of our item's stack type
                // (tests items in the cell, as well as reservations on the cell)
                bool cellIsSameStackType = false;
                if (validDestination)
                    foreach (var i in itemsInCell.Where(i => Record.CanMixWith(i)))
                        cellIsSameStackType = true;
                Pawn claimant = map.reservationManager.FirstRespectedReserver(cell, carrier);
                if (claimant != null)
                {
                    List<Job> jobs =
                    [
                        ..claimant.jobs.jobQueue.Select(x => x.job),
                        claimant.jobs.curJob
                    ];
                    if (Enumerable.Any(jobs, job => job.def.driverClass == typeof(JobDriver_HaulExplicitly)
                                                    && (job.targetB == cell || job.targetQueueB.Contains(cell))
                                                    && (Record.CanMixWith(job.targetA.Thing))))
                    {
                        cellIsSameStackType = true;
                    }
                }

                //finally, increment our counter of cells with our item's stack type
                if (cellIsSameStackType)
                {
                    _destsWithThisStackType++;
                }

                //check if cell is valid, reachable from item, unreserved, and pawn is allowed to go there
                bool reachable = map.reachability.CanReach(itemPos, cell, PathEndMode.ClosestTouch, traverseparms);
                if (!validDestination || !reachable || claimant != null || cell.IsForbidden(carrier))
                {
                    continue;
                }

                // oh, just item things
                if (itemsInCell.Count == 0)
                {
                    FreeCells.Add(cell);
                }

                try
                {
                    Thing itemInCell = itemsInCell.Single();
                    int spaceAvail = itemInCell.def.stackLimit - itemInCell.stackCount;
                    if (cellIsSameStackType && spaceAvail > 0)
                    {
                        PartialCells.Add(cell);
                        PartialCellSpaceAvailable.Add(spaceAvail);
                    }
                }
                catch
                {
                }
                /*Thing itemInCell = itemsInCell.Single();
                int spaceAvail = itemInCell.def.stackLimit - itemInCell.stackCount;
                if (cellIsSameStackType && spaceAvail > 0)
                {
                    PartialCells.Add(cell);
                    PartialCellSpaceAvailable.Add(spaceAvail);
                }*/
            }
        }

        public static DeliverableDestinations For(Thing item, Pawn carrier, HaulExplicitlyPosting posting = null, Func<IntVec3, float> grader = null)
        {
            if (posting == null) //do the handholdy version of this function
            {
                posting = HaulExplicitly.GetManager(item).PostingWithItem(item);
                if (posting == null)
                    throw new ArgumentException();
            }

            return new DeliverableDestinations(item, carrier, posting, (grader != null) ? grader : DefaultGrader);
        }

        public static float DefaultGrader(IntVec3 c)
        {
            return 0.0f;
        }

        public List<IntVec3> UsableDests()
        {
            int freeCellsWillUse = Math.Min(FreeCells.Count,
                Math.Max(0, Record.NumStacksWillUse - _destsWithThisStackType));
            List<IntVec3> result = new List<IntVec3>(PartialCells);
            result.AddRange(
                FreeCells.OrderByDescending(_grader)
                    .Take(freeCellsWillUse));
            return result;
        }

        public List<IntVec3> RequestSpaceForItemAmount(int amount)
        {
            List<IntVec3> usableDests = UsableDests();
            if (usableDests.Count == 0)
                return new List<IntVec3>();
            var destinationsOrdered = new List<IntVec3>(ProximityOrdering(usableDests.RandomElement(), usableDests));
            int u; //number of dests to use
            int destinationSpaceAvailable = 0;
            for (u = 0; u < destinationsOrdered.Count && destinationSpaceAvailable < amount; u++)
            {
                int i = PartialCells.IndexOf(destinationsOrdered[u]);
                int space = (i == -1) ? _thing.def.stackLimit : PartialCellSpaceAvailable[i];
                destinationSpaceAvailable += space;
            }

            return [..destinationsOrdered.Take(u)];
        }

        public int FreeSpaceInCells(IEnumerable<IntVec3> cells)
        {
            int space = 0;
            foreach (var c in cells)
            {
                if (!PartialCells.Contains(c) && !FreeCells.Contains(c))
                {
                    throw new ArgumentException("Specified cells don't exist in DeliverableDestinations.");
                }

                var thingsAtCell = Posting.Map.thingGrid.ThingsAt(c).ToList();
                if (!thingsAtCell.Any())
                {
                    return space += _thing.def.stackLimit;
                }

                if (thingsAtCell.Any(t => t.def.category == ThingCategory.Plant))
                {
                    return space += _thing.def.stackLimit;
                }

                var item = thingsAtCell.First(t => t.def.EverStorable(false));
                return space += _thing.def.stackLimit - item.stackCount;
            }

            return 0;
        }

        private static IEnumerable<IntVec3> ProximityOrdering(IntVec3 center, IEnumerable<IntVec3> cells)
        {
            return cells.OrderBy(c => Math.Abs(center.x - c.x) + Math.Abs(center.y - c.y));
        }
    }
}