namespace FiveKingdoms.Core
{
    /// <summary>
    /// Elements from GAME_PLAN.md: five base elements and their advanced forms (Wind's is still to be decided). Darkness
    /// also covers time, cosmic and space magic. Elemental damage is cut by the target's resistance to that element;
    /// resistances and the element chart come with 1j.
    /// </summary>
    public enum Element { None, Fire, Water, Wind, Earth, Darkness, Lightning, Ice, Metal }

    /// <summary>Physical or magic. Both are reduced by DEF; sets and weapons key off the difference.</summary>
    public enum DamageKind { Physical, Magic }

    /// <summary>How an attack reaches its target. Sets and weapons key off it (e.g. Archer's Garb boosts ranged skills).</summary>
    public enum AttackReach { Melee, Ranged, Area }
}
