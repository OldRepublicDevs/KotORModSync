// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/NameGenerator.java:15-889
// Original: public class NameGenerator
using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;


namespace KOTORModSync.Formats.NCS.NCSDecomp.Scriptutils
{
    public class NameGenerator
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/NameGenerator.java:16-25
        // Original: private static String actionParamTag(AExpression in) { ... }
        private static string ActionParamTag(object @in)
        {
            if (@in is AConst)
            {
                string str = ((AConst)@in).ToString();
                if (str.Length > 2)
                {
                    return (char.ToUpper(str[1]) + str.Substring(2, str.Length - 1)).Replace(' ', '_');
                }
            }

            return null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/NameGenerator.java:27-29
        // Original: private static int actionParamToInt(AExpression in) { return AConst.class.isInstance(in) ? Integer.parseInt(((AConst)in).toString()) : -1; }
        private static int ActionParamToInt(object @in)
        {
            if (@in is AConst)
            {
                return Integer.ParseInt(((AConst)@in).ToString());
            }

            return -1;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptutils/NameGenerator.java:31-48
        // Original: public static String getNameFromAction(AActionExp actionexp) { ... }
        public static string GetNameFromAction(AActionExp actionexp)
        {
            string action = actionexp.GetAction();
            if (action.Equals("GetObjectByTag"))
            {
                string tag = ActionParamTag(actionexp.GetParam(0));
                if (tag != null)
                {
                    return "o" + tag;
                }

                return null;
            }
            else
            {
                if (action.Equals("GetFirstPC"))
                {
                    return "oPC";
                }

                if (action.Equals("GetScriptParameter"))
                {
                    int i = ActionParamToInt(actionexp.GetParam(0));
                    if (i > 0)
                    {
                        return "nParam" + i;
                    }

                    return "nParam";
                }
                else
                {
                    if (action.Equals("GetScriptStringParameter"))
                    {
                        return "sParam";
                    }

                    if (action.Equals("GetMaxHitPoints"))
                    {
                        return "nMaxHP";
                    }

                    if (action.Equals("GetCurrentHitPoints"))
                    {
                        return "nCurHP";
                    }

                    if (action.Equals("Random"))
                    {
                        return "nRandom";
                    }

                    if (action.Equals("GetArea"))
                    {
                        return "oArea";
                    }

                    if (action.Equals("GetEnteringObject"))
                    {
                        return "oEntering";
                    }

                    if (action.Equals("GetExitingObject"))
                    {
                        return "oExiting";
                    }

                    if (action.Equals("GetPosition"))
                    {
                        return "vPosition";
                    }

                    if (action.Equals("GetFacing"))
                    {
                        return "fFacing";
                    }

                    if (action.Equals("GetLastAttacker"))
                    {
                        return "oAttacker";
                    }

                    if (action.Equals("GetNearestCreature"))
                    {
                        return "oNearest";
                    }

                    if (action.Equals("GetDistanceToObject"))
                    {
                        return "fDistance";
                    }

                    if (action.Equals("GetIsObjectValid"))
                    {
                        return "nValid";
                    }

                    if (action.Equals("GetSpellTargetObject"))
                    {
                        return "oTarget";
                    }

                    if (action.Equals("EffectAssuredHit"))
                    {
                        return "efHit";
                    }

                    if (action.Equals("GetLastItemEquipped"))
                    {
                        return "oLastEquipped";
                    }

                    if (action.Equals("GetCurrentForcePoints"))
                    {
                        return "nCurFP";
                    }

                    if (action.Equals("GetMaxForcePoints"))
                    {
                        return "nMaxFP";
                    }

                    if (action.Equals("EffectHeal"))
                    {
                        return "efHeal";
                    }

                    if (action.Equals("EffectDamage"))
                    {
                        return "efDamage";
                    }

                    if (action.Equals("EffectAbilityIncrease"))
                    {
                        return "efAbilityInc";
                    }

                    if (action.Equals("EffectDamageResistance"))
                    {
                        return "efDamageRes";
                    }

                    if (action.Equals("EffectResurrection"))
                    {
                        return "efResurrect";
                    }

                    if (action.Equals("GetCasterLevel"))
                    {
                        return "nCasterLevel";
                    }

                    if (action.Equals("GetFirstObjectInArea"))
                    {
                        return "oAreaObject";
                    }

                    if (action.Equals("GetNextObjectInArea"))
                    {
                        return "oAreaObject";
                    }

                    if (action.Equals("GetObjectType"))
                    {
                        return "nType";
                    }

                    if (action.Equals("GetRacialType"))
                    {
                        return "nRace";
                    }

                    if (action.Equals("EffectACIncrease"))
                    {
                        return "efACInc";
                    }

                    if (action.Equals("EffectSavingThrowIncrease"))
                    {
                        return "efSaveInc";
                    }

                    if (action.Equals("EffectAttackIncrease"))
                    {
                        return "efAttackInc";
                    }

                    if (action.Equals("EffectDamageReduction"))
                    {
                        return "efDamageDec";
                    }

                    if (action.Equals("EffectDamageIncrease"))
                    {
                        return "efDamageInc";
                    }

                    if (action.Equals("GetGoodEvilValue"))
                    {
                        return "nAlign";
                    }

                    if (action.Equals("GetPartyMemberCount"))
                    {
                        return "nPartyCount";
                    }

                    if (action.Equals("GetAlignmentGoodEvil"))
                    {
                        return "nAlign";
                    }

                    if (action.Equals("GetFirstObjectInShape"))
                    {
                        return "oShapeObject";
                    }

                    if (action.Equals("GetNextObjectInShape"))
                    {
                        return "oShapeObject";
                    }

                    if (action.Equals("EffectEntangle"))
                    {
                        return "efEntangle";
                    }

                    if (action.Equals("EffectDeath"))
                    {
                        return "efDeath";
                    }

                    if (action.Equals("EffectKnockdown"))
                    {
                        return "efKnockdown";
                    }

                    if (action.Equals("GetAbilityScore"))
                    {
                        int i = ActionParamToInt(actionexp.GetParam(1));
                        switch (i)
                        {
                            case 0:
                                {
                                    return "nStrength";
                                }

                            case 1:
                                {
                                    return "nDex";
                                }

                            case 2:
                                {
                                    return "nConst";
                                }

                            case 3:
                                {
                                    return "nInt";
                                }

                            case 4:
                                {
                                    return "nWis";
                                }

                            case 5:
                                {
                                    return "nChar";
                                }

                            default:
                                {
                                    return "nAbility";
                                }
                        }
                    }
                    else
                    {
                        if (action.Equals("EffectParalyze"))
                        {
                            return "efParalyze";
                        }

                        if (action.Equals("EffectSpellImmunity"))
                        {
                            return "efSpellImm";
                        }

                        if (action.Equals("GetDistanceBetween"))
                        {
                            return "fDistance";
                        }

                        if (action.Equals("EffectForceJump"))
                        {
                            return "efForceJump";
                        }

                        if (action.Equals("EffectSleep"))
                        {
                            return "efSleep";
                        }

                        if (action.Equals("GetItemInSlot"))
                        {
                            int i = ActionParamToInt(actionexp.GetParam(0));
                            switch (i)
                            {
                                case 0:
                                    {
                                        return "oHeadItem";
                                    }

                                case 1:
                                    {
                                        return "oBodyItem";
                                    }

                                case 3:
                                    {
                                        return "oHandsItem";
                                    }

                                case 4:
                                    {
                                        return "oRWeapItem";
                                    }

                                case 5:
                                    {
                                        return "oLWeapItem";
                                    }

                                case 7:
                                    {
                                        return "oLArmItem";
                                    }

                                case 8:
                                    {
                                        return "oRArmItem";
                                    }

                                case 9:
                                    {
                                        return "oImplantItem";
                                    }

                                case 10:
                                    {
                                        return "oBeltItem";
                                    }

                                case 14:
                                    {
                                        return "oCWeapLItem";
                                    }

                                case 15:
                                    {
                                        return "oCWeapRItem";
                                    }

                                case 16:
                                    {
                                        return "oCWeapBItem";
                                    }

                                case 17:
                                    {
                                        return "oCArmourItem";
                                    }

                                case 18:
                                    {
                                        return "oRWeap2Item";
                                    }

                                case 19:
                                    {
                                        return "oLWeap2Item";
                                    }

                                default:
                                    {
                                        return "oSlotItem";
                                    }

                            }
                        }
                        else
                        {
                            if (action.Equals("EffectTemporaryForcePoints"))
                            {
                                return "efTempFP";
                            }

                            if (action.Equals("EffectConfused"))
                            {
                                return "efConfused";
                            }

                            if (action.Equals("EffectFrightened"))
                            {
                                return "efFright";
                            }

                            if (action.Equals("EffectChoke"))
                            {
                                return "efChoke";
                            }

                            if (action.Equals("EffectStunned"))
                            {
                                return "efStun";
                            }

                            if (action.Equals("EffectRegenerate"))
                            {
                                return "efRegen";
                            }

                            if (action.Equals("EffectMovementSpeedIncrease"))
                            {
                                return "efSpeedInc";
                            }

                            if (action.Equals("GetHitDice"))
                            {
                                return "nLevel";
                            }

                            if (action.Equals("GetEffectType"))
                            {
                                return "nEfType";
                            }

                            if (action.Equals("EffectAreaOfEffect"))
                            {
                                return "efAOE";
                            }

                            if (action.Equals("EffectVisualEffect"))
                            {
                                return "efVisual";
                            }

                            if (action.Equals("GetFactionWeakestMember"))
                            {
                                return "oWeakest";
                            }

                            if (action.Equals("GetFactionStrongestMember"))
                            {
                                return "oStrongest";
                            }

                            if (action.Equals("GetFactionMostDamagedMember"))
                            {
                                return "oMostDamaged";
                            }

                            if (action.Equals("GetFactionLeastDamagedMember"))
                            {
                                return "oLeastDamaged";
                            }

                            if (action.Equals("GetWaypointByTag"))
                            {
                                string tag = ActionParamTag(actionexp.GetParam(0));
                                if (tag != null)
                                {
                                    return "o" + tag;
                                }

                                return "oWP";
                            }
                            else
                            {
                                if (action.Equals("GetTransitionTarget"))
                                {
                                    return "oTransTarget";
                                }

                                if (action.Equals("EffectBeam"))
                                {
                                    return "efBeam";
                                }

                                if (action.Equals("GetReputation"))
                                {
                                    return "nRep";
                                }

                                if (action.Equals("GetModuleFileName"))
                                {
                                    return "sModule";
                                }

                                if (action.Equals("EffectForceResistanceIncrease"))
                                {
                                    return "efForceResInc";
                                }

                                if (action.Equals("GetSpellTargetLocation"))
                                {
                                    return "locTarget";
                                }

                                if (action.Equals("EffectBodyFuel"))
                                {
                                    return "efFuel";
                                }

                                if (action.Equals("GetFacingFromLocation"))
                                {
                                    return "fFacing";
                                }

                                if (action.Equals("GetNearestCreatureToLocation"))
                                {
                                    return "oNearestCreat";
                                }

                                if (action.Equals("GetNearestObject"))
                                {
                                    return "oNearest";
                                }

                                if (action.Equals("GetNearestObjectToLocation"))
                                {
                                    return "oNearest";
                                }

                                if (action.Equals("GetNearestObjectByTag"))
                                {
                                    string tag = ActionParamTag(actionexp.GetParam(0));
                                    if (tag != null)
                                    {
                                        return "oNearest" + tag;
                                    }

                                    return null;
                                }
                                else
                                {
                                    if (action.Equals("GetPCSpeaker"))
                                    {
                                        return "oSpeaker";
                                    }

                                    if (action.Equals("GetModule"))
                                    {
                                        return "oModule";
                                    }

                                    if (action.Equals("CreateObject"))
                                    {
                                        string tag = ActionParamTag(actionexp.GetParam(1));
                                        if (tag != null)
                                        {
                                            return "o" + tag;
                                        }

                                        return null;
                                    }
                                    else
                                    {
                                        if (action.Equals("EventSpellCastAt"))
                                        {
                                            return "evSpellCast";
                                        }

                                        if (action.Equals("GetLastSpellCaster"))
                                        {
                                            return "oCaster";
                                        }

                                        if (action.Equals("EffectPoison"))
                                        {
                                            return "efPoison";
                                        }

                                        if (action.Equals("EffectAssuredDeflection"))
                                        {
                                            return "efDeflect";
                                        }

                                        if (action.Equals("GetName"))
                                        {
                                            return "sName";
                                        }

                                        if (action.Equals("GetLastSpeaker"))
                                        {
                                            return "oSpeaker";
                                        }

                                        if (action.Equals("GetLastPerceived"))
                                        {
                                            return "oPerceived";
                                        }

                                        if (action.Equals("EffectForcePushTargeted"))
                                        {
                                            return "efPush";
                                        }

                                        if (action.Equals("EffectHaste"))
                                        {
                                            return "efHaste";
                                        }

                                        if (action.Equals("EffectImmunity"))
                                        {
                                            return "efImmunity";
                                        }

                                        if (action.Equals("GetIsImmune"))
                                        {
                                            return "nImmune";
                                        }

                                        if (action.Equals("EffectDamageImmunityIncrease"))
                                        {
                                            return "efDamageImmInc";
                                        }

                                        if (action.Equals("GetDistanceBetweenLocations"))
                                        {
                                            return "fDistance";
                                        }

                                        if (action.Equals("GetLocalNumber"))
                                        {
                                            return "nLocal";
                                        }

                                        if (action.Equals("GetStringLength"))
                                        {
                                            return "nLen";
                                        }

                                        if (action.Equals("GetObjectPersonalSpace"))
                                        {
                                            return "fPersonalSpace";
                                        }

                                        if (action.Equals("d8"))
                                        {
                                            return "nRandom";
                                        }

                                        if (action.Equals("d10"))
                                        {
                                            return "nRandom";
                                        }

                                        if (action.Equals("GetPartyMemberByIndex"))
                                        {
                                            return "oNPC";
                                        }

                                        if (action.Equals("GetAttackTarget"))
                                        {
                                            return "oTarget";
                                        }

                                        if (action.Equals("GetCreatureTalentRandom"))
                                        {
                                            return "talRandom";
                                        }

                                        if (action.Equals("GetPUPOwner"))
                                        {
                                            return "oPUPOwner";
                                        }

                                        if (action.Equals("GetDistanceToObject2D"))
                                        {
                                            return "fDistance";
                                        }

                                        if (action.Equals("GetCurrentAction"))
                                        {
                                            return "nAction";
                                        }

                                        if (action.Equals("GetPartyLeader"))
                                        {
                                            return "oLeader";
                                        }

                                        if (action.Equals("GetFirstEffect"))
                                        {
                                            return "efFirst";
                                        }

                                        if (action.Equals("GetNextEffect"))
                                        {
                                            return "efNext";
                                        }

                                        if (action.Equals("GetPartyAIStyle"))
                                        {
                                            return "nStyle";
                                        }

                                        if (action.Equals("GetNPCAIStyle"))
                                        {
                                            return "nNPCStyle";
                                        }

                                        if (action.Equals("GetLastHostileTarget"))
                                        {
                                            return "oLastTarget";
                                        }

                                        if (action.Equals("GetLastHostileActor"))
                                        {
                                            return "oLastActor";
                                        }

                                        if (action.Equals("GetRandomDestination"))
                                        {
                                            return "vRandom";
                                        }

                                        if (action.Equals("Location"))
                                        {
                                            return null;
                                        }

                                        if (action.Equals("GetHealTarget"))
                                        {
                                            return "oTarget";
                                        }

                                        if (action.Equals("GetCreatureTalentBest"))
                                        {
                                            return "talBest";
                                        }

                                        if (action.Equals("d4"))
                                        {
                                            return "nRandom";
                                        }

                                        if (action.Equals("d6"))
                                        {
                                            return "nRandom";
                                        }

                                        if (action.Equals("d100"))
                                        {
                                            return "nRandom";
                                        }

                                        if (action.Equals("d3"))
                                        {
                                            return "nRandom";
                                        }

                                        if (action.Equals("GetIdFromTalent"))
                                        {
                                            return "nTalent";
                                        }

                                        if (action.Equals("GetLocalBoolean"))
                                        {
                                            return "nLocalBool";
                                        }

                                        if (action.Equals("TalentSpell"))
                                        {
                                            return "talSpell";
                                        }

                                        if (action.Equals("TalentFeat"))
                                        {
                                            return "talFeat";
                                        }

                                        if (action.Equals("FloatToString"))
                                        {
                                            return null;
                                        }

                                        if (action.Equals("GetLocation"))
                                        {
                                            return null;
                                        }

                                        if (action.Equals("IntToString"))
                                        {
                                            return null;
                                        }

                                        if (action.Equals("GetGlobalNumber"))
                                        {
                                            return "nGlobal";
                                        }

                                        if (action.Equals("GetBaseItemType"))
                                        {
                                            return "nItemType";
                                        }

                                        if (action.Equals("GetFirstItemInInventory"))
                                        {
                                            return "oInvItem";
                                        }

                                        if (action.Equals("GetNextItemInInventory"))
                                        {
                                            return "oInvItem";
                                        }

                                        if (action.Equals("GetSpellBaseForcePointCost"))
                                        {
                                            return "nBaseFP";
                                        }

                                        if (action.Equals("GetLastForcePowerUsed"))
                                        {
                                            return "nLastForce";
                                        }

                                        if (action.Equals("StringToInt"))
                                        {
                                            return null;
                                        }

                                        Debug("Variable Naming: consider adding " + action);
                                        return null;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}




