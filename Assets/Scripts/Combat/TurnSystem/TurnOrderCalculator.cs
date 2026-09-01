using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using KAM.Core.Combat;

namespace KAM.Combat.TurnSystem
{
    /// <summary>
    /// Calculates and manages turn order based on unit speed.
    /// Handles speed modifiers from buffs/debuffs.
    /// Based on GDD 1.7.2 Turn Order System.
    /// </summary>
    public class TurnOrderCalculator
    {
        /// <summary>
        /// Calculate turn order for all units based on their effective speed.
        /// Higher speed = earlier in turn order.
        /// </summary>
        public List<CombatUnit> CalculateTurnOrder(List<CombatUnit> units)
        {
            if (units == null || units.Count == 0)
                return new List<CombatUnit>();

            // Sort by effective speed (descending - faster units go first)
            // Use unit name as tiebreaker for consistent ordering
            return units
                .Where(u => u != null && u.IsAlive)
                .OrderByDescending(u => GetEffectiveSpeed(u))
                .ThenBy(u => u.IsPlayer ? 0 : 1)  // Player wins ties
                .ThenBy(u => u.UnitName)
                .ToList();
        }

        /// <summary>
        /// Get a unit's effective speed (base speed * modifiers).
        /// </summary>
        public int GetEffectiveSpeed(CombatUnit unit)
        {
            if (unit == null)
                return 0;

            return unit.GetEffectiveSpeedWithEffects();
        }

        /// <summary>
        /// Insert a new unit into an existing turn queue at the appropriate position.
        /// Used when units are summoned mid-combat.
        /// </summary>
        public void InsertIntoQueue(Queue<CombatUnit> queue, CombatUnit newUnit, List<CombatUnit> allUnits)
        {
            if (queue == null || newUnit == null) return;

            int newUnitSpeed = GetEffectiveSpeed(newUnit);

            // Convert queue to list for manipulation
            List<CombatUnit> queueList = queue.ToList();
            queue.Clear();

            // Find insertion point
            int insertIndex = 0;
            for (int i = 0; i < queueList.Count; i++)
            {
                if (GetEffectiveSpeed(queueList[i]) < newUnitSpeed)
                {
                    insertIndex = i;
                    break;
                }
                insertIndex = i + 1;
            }

            // Insert and rebuild queue
            queueList.Insert(insertIndex, newUnit);
            foreach (var unit in queueList)
            {
                queue.Enqueue(unit);
            }
        }

        /// <summary>
        /// Recalculate positions in an existing queue when speeds change.
        /// </summary>
        public void RecalculateQueue(Queue<CombatUnit> queue)
        {
            if (queue == null || queue.Count == 0) return;

            List<CombatUnit> units = queue.ToList();
            queue.Clear();

            var reordered = CalculateTurnOrder(units);
            foreach (var unit in reordered)
            {
                queue.Enqueue(unit);
            }
        }

        /// <summary>
        /// Get the position of a unit in turn order (1-indexed).
        /// </summary>
        public int GetTurnPosition(List<CombatUnit> turnOrder, CombatUnit unit)
        {
            int index = turnOrder.IndexOf(unit);
            return index >= 0 ? index + 1 : -1;
        }

        /// <summary>
        /// Calculate how many turns until a specific unit acts.
        /// </summary>
        public int GetTurnsUntilAction(Queue<CombatUnit> queue, CombatUnit unit)
        {
            if (queue == null || unit == null) return -1;

            int position = 0;
            foreach (var queuedUnit in queue)
            {
                if (queuedUnit == unit)
                    return position;
                position++;
            }

            return -1; // Unit not in queue
        }

        /// <summary>
        /// Preview what the turn order would look like with a speed change.
        /// Useful for UI to show effect of speed buffs/debuffs.
        /// </summary>
        public List<CombatUnit> PreviewOrderWithSpeedChange(List<CombatUnit> units, CombatUnit affectedUnit, float speedMultiplier)
        {
            if (units == null || affectedUnit == null)
                return CalculateTurnOrder(units);

            // Store original modifier
            float originalModifier = affectedUnit.Stats.SpeedModifier;

            // Temporarily apply new modifier
            affectedUnit.Stats.SpeedModifier = speedMultiplier;

            // Calculate new order
            var newOrder = CalculateTurnOrder(units);

            // Restore original
            affectedUnit.Stats.SpeedModifier = originalModifier;

            return newOrder;
        }

        /// <summary>
        /// Check if a unit would go before another unit with current speeds.
        /// </summary>
        public bool WouldActBefore(CombatUnit unit, CombatUnit otherUnit)
        {
            if (unit == null || otherUnit == null) return false;

            int unitSpeed = GetEffectiveSpeed(unit);
            int otherSpeed = GetEffectiveSpeed(otherUnit);

            if (unitSpeed != otherSpeed)
                return unitSpeed > otherSpeed;

            // Tiebreaker: player goes first
            return unit.IsPlayer && !otherUnit.IsPlayer;
        }

        /// <summary>
        /// Get speed comparison info for UI display.
        /// </summary>
        public SpeedComparisonInfo CompareSpeed(CombatUnit unit, List<CombatUnit> allUnits)
        {
            var info = new SpeedComparisonInfo();

            if (unit == null || allUnits == null)
                return info;

            int unitSpeed = GetEffectiveSpeed(unit);

            info.UnitSpeed = unitSpeed;
            info.FasterUnits = allUnits.Count(u => u != unit && u.IsAlive && GetEffectiveSpeed(u) > unitSpeed);
            info.SlowerUnits = allUnits.Count(u => u != unit && u.IsAlive && GetEffectiveSpeed(u) < unitSpeed);
            info.SameSpeedUnits = allUnits.Count(u => u != unit && u.IsAlive && GetEffectiveSpeed(u) == unitSpeed);

            var ordered = CalculateTurnOrder(allUnits);
            info.TurnPosition = GetTurnPosition(ordered, unit);

            return info;
        }
    }

    /// <summary>
    /// Information about a unit's speed relative to others.
    /// </summary>
    public struct SpeedComparisonInfo
    {
        public int UnitSpeed;
        public int FasterUnits;
        public int SlowerUnits;
        public int SameSpeedUnits;
        public int TurnPosition;
    }
}
