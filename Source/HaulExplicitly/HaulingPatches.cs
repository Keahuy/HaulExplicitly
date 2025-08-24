﻿using Verse;
using Verse.AI;
using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace HaulExplicitly
{
    [StaticConstructorOnStartup]
    public class PatchMain
    {
        public static Harmony instance;

        static PatchMain()
        {
            instance = new Harmony("likeafox.rimworld.haulexplicitly");
            instance.PatchAll(Assembly.GetExecutingAssembly()); 
        }
        
        public static void DoPatching()
        {
            var harmony = new Harmony("likeafox.rimworld.haulexplicitly");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(GridsUtility), "GetFirstHaulable")]
    class GridsUtility_GetFirstHaulable_Patch
    {
        static void Postfix(ref Thing __result, IntVec3 c, Map map)
        {
            List<Thing> list = map.thingGrid.ThingsListAt(c);
            for (int i = 0; i < list.Count; i++)
                if (list[i].def.EverHaulable)
                {
                    __result = list[i];
                    return;
                }

            __result = null;
        }
    }

    [HarmonyPatch(typeof(HaulAIUtility), "PawnCanAutomaticallyHaul")]
    class HaulAIUtility_PawnCanAutomaticallyHaul_Patch
    {
        static bool Prefix(Pawn p, Thing t, bool forced, ref bool __result)
        {
            __result = false;
            if (!t.def.EverHaulable)
                return false;
            if (t.IsForbidden(p))
            {
                if (!t.Position.InAllowedArea(p))
                    JobFailReason.Is("ForbiddenOutsideAllowedAreaLower".Translate(), null);
                else
                    JobFailReason.Is("ForbiddenLower".Translate(), null);
                return false;
            }

            __result = Verse.AI.HaulAIUtility.PawnCanAutomaticallyHaulFast(p, t, forced);
            return false;
        }
    }

    [HarmonyPatch(typeof(HaulAIUtility), "PawnCanAutomaticallyHaulFast")]
    class HaulAIUtility_PawnCanAutomaticallyHaulFast_Patch
    {
        static void Postfix(Thing t, ref bool __result)
        {
            __result = __result && t.IsAHaulableSetToHaulable();
        }
    }

    [HarmonyPatch(typeof(ListerHaulables), "ShouldBeHaulable")]
    class ListerHaulables_ShouldBeHaulable_Patch
    {
        static bool Prefix(Thing t, ListerHaulables __instance, ref bool __result)
        {
            __result = __instance.ShouldBeHaulableExt(t);
            return false;
        }
    }

    [HarmonyPatch(typeof(ListerHaulables), "HaulDesignationAdded")]
    class ListerHaulables_HaulDesignationAdded_Patch
    {
        static bool Prefix(Thing t)
        {
            typeof(ListerHaulables).GetMethod("Check", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(t.MapHeld.listerHaulables, new object[] { t });
            return false;
        }
    }

    [HarmonyPatch(typeof(ListerHaulables), "HaulDesignationRemoved")]
    class ListerHaulables_HaulDesignationRemoved_Patch
    {
        static bool Prefix(Thing t, ListerHaulables __instance)
        {
            bool shouldhave = t.SpawnedOrAnyParentSpawned &&
                              __instance.ShouldBeHaulableExt(t, true);
            List<Thing> haulables =
                (List<Thing>)typeof(ListerHaulables).InvokeMember("haulables",
                    BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic,
                    null, __instance, null);

            if (shouldhave != haulables.Contains(t))
            {
                if (shouldhave)
                    haulables.Add(t);
                else
                    haulables.Remove(t);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(DesignationManager), "TryRemoveDesignationOn")]
    class DesignationManager_TryRemoveDesignationOn_Patch
    {
        //no longer shall haul designation be removed when a pawn puts an item down
        static bool Prefix(DesignationDef def)
        {
            if (def != DesignationDefOf.Haul) return true;
            return MiscUtil.StackFrameWithMethod("PlaceHauledThingInCell") == null;
        }
    }

    public static class HaulPatchState
    {
        public static DesignationDef last_des_type_try_add = null;
    }

    [HarmonyPatch(typeof(DesignationManager), "AddDesignation")]
    class DesignationManager_AddDesignation_Patch
    {
        static void Prefix(Designation newDes)
        {
            HaulPatchState.last_des_type_try_add = newDes.def;
        }
    }

    [HarmonyPatch(typeof(ForbidUtility), "SetForbidden")]
    class ForbidUtility_SetForbidden_Patch
    {
        //adding haul designation shall no longer change forbiddenness
        static bool Prefix(bool value, bool warnOnFail)
        {
            if (value || warnOnFail) return true;
            return (HaulPatchState.last_des_type_try_add != DesignationDefOf.Haul ||
                    MiscUtil.StackFrameWithMethod("AddDesignation") == null);
        }
    }

    [HarmonyPatch(typeof(ListerMergeables), "ShouldBeMergeable")]
    class ListerMergeables_ShouldBeMergeable_Patch
    {
        static void Postfix(Thing t, ref bool __result)
        {
            __result = __result && t.IsAHaulableSetToHaulable();
        }
    }

    [HarmonyPatch(typeof(Designation), "Notify_Added")]
    class Designation_Notify_Added_Patch
    {
        static void Postfix(Designation __instance)
        {
            if (__instance.def == DesignationDefOf.Haul)
                __instance.designationManager.map.listerMergeables
                    .Notify_ThingStackChanged(__instance.target.Thing);
        }
    }

    [HarmonyPatch(typeof(Designation), "Notify_Removing")]
    class Designation_Notify_Removing_Patch
    {
        static void Postfix(Designation __instance)
        {
            if (__instance.def == DesignationDefOf.Haul && __instance.target.HasThing)
                __instance.designationManager.map.listerMergeables
                    .Notify_ThingStackChanged(__instance.target.Thing);
        }
    }

    [HarmonyPatch(typeof(JobDriver), "SetupToils")]
    class JobDriver_SetupToils_Patch
    {
        //Stop a hauling job if haulability gets toggled off
        static void Postfix(JobDriver __instance)
        {
            if (__instance is JobDriver_HaulToCell)
            {
                List<Toil> toils =
                    (List<Toil>)typeof(JobDriver).InvokeMember("toils",
                        BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic,
                        null, __instance, null);
                Toil toilGoto = toils[
#if !RW_1_4_OR_GREATER
                    1
#elif RW_1_4
                        3
#elif RW_1_5
                        4
#endif
                ];
                if (toilGoto.defaultCompleteMode != ToilCompleteMode.PatherArrival)
                {
                    Log.Error("Trying to add fail condition on JobDriver_HaulToCell but "
                              + toilGoto.ToString()
                              + " doesn't appear to be a Goto Toil");
                    return;
                }

                toilGoto.AddFailCondition(delegate
                {
                    Thing t = __instance.job.targetA.Thing;
                    return !__instance.job.ignoreDesignations && !t.IsAHaulableSetToHaulable();
                });
            }
        }
    }

#if RW_1_0 || RW_1_1 || RW_1_2
    [HarmonyPatch(typeof(Toils_Haul), "CheckForGetOpportunityDuplicate")]
    class Toils_Haul_CheckForGetOpportunityDuplicate_Patch
    {
        static void Prefix(ref Predicate<Thing> extraValidator)
        {
            const string sf_namepart =
#if RW_1_0
                "JobDriver_HaulToCell";
#else
                "<MakeNewToils>";
#endif
            System.Diagnostics.StackFrame sf = MiscUtil.StackFrameWithMethod(sf_namepart, 3);
            if (sf == null)
                return;
#if !RW_1_0
            var locals = sf.GetMethod().GetMethodBody().LocalVariables;
            bool driverInLocals = locals.Select(l => l.LocalType)
                                        .Where(lt => lt == typeof(Verse.AI.JobDriver_HaulToCell))
                                        .Any();
            if (!driverInLocals)
                return;
#endif

            var other_test = extraValidator;
            Predicate<Thing> test = delegate (Thing t)
            {
                return t.IsAHaulableSetToHaulable()
                    && (other_test == null || other_test(t));
            };
            extraValidator = test;
        }
    }
#endif

#if RW_1_3 || RW_1_4 || RW_1_5
    [HarmonyPatch]
    class JobDriver_HaulToCell_MakeNewToils_Patch
    {
        [HarmonyTargetMethod]
        public static MethodBase Get_MakeNewToils_MoveNext()
        {
            Type makenewtoils = typeof(JobDriver_HaulToCell).GetNestedTypes(AccessTools.all)
                .First(t => t.Name.Contains("MakeNewToils"));
            return AccessTools.Method(makenewtoils, "MoveNext");
        }

        private static Predicate<Thing> validator = HaulablesUtilities.IsAHaulableSetToHaulable;

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int patch_count = 0;
            CodeInstruction[] queue = { null, null };
            IEnumerator<CodeInstruction> inst_enum = instructions.GetEnumerator();
            inst_enum.MoveNext();
            queue[0] = inst_enum.Current;
            inst_enum.MoveNext();
            queue[1] = inst_enum.Current;

            while (queue[0] != null)
            {
                if (queue[1]?.Calls(typeof(Toils_Haul).GetMethod("CheckForGetOpportunityDuplicate")) == true)
                {
                    if (queue[0].opcode != System.Reflection.Emit.OpCodes.Ldnull)
                        Log.Error("Replacing existing validator");
                    var fld = typeof(JobDriver_HaulToCell_MakeNewToils_Patch).GetField("validator", AccessTools.all);
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldsfld, fld);
                    patch_count++;
                }
                else
                {
                    yield return queue[0];
                }

                queue[0] = queue[1];
                if (inst_enum.MoveNext())
                    queue[1] = inst_enum.Current;
                else
                    queue[1] = null;
            }

            if (patch_count != 1)
                Log.Error("Patch logic invoked non-1 times: " + patch_count.ToString());
        }
    }
#endif
}