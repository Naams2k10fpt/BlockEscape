using UnityEngine;

namespace BlockEscape.Core
{
    public readonly struct DamageInfo
    {
        public int Amount { get; }
        public Vector2 Knockback { get; }
        public GameObject Source { get; }
        public DamageType Type { get; }

        public DamageInfo(int amount, Vector2 knockback, GameObject source, DamageType type)
        {
            Amount = Mathf.Max(0, amount);
            Knockback = knockback;
            Source = source;
            Type = type;
        }
    }
}
