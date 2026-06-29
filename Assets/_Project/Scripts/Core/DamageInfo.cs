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

    internal static class PhysicsMaterialLibrary
    {
        private static PhysicsMaterial2D _frictionless;

        public static PhysicsMaterial2D Frictionless
        {
            get
            {
                if (_frictionless != null)
                    return _frictionless;

                _frictionless = new PhysicsMaterial2D("Runtime Frictionless")
                {
                    friction = 0f,
                    bounciness = 0f
                };
                return _frictionless;
            }
        }
    }
}
