using System;

namespace effectshud.src
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class EffectRegistrationAttribute : Attribute
    {
        public string TypeId { get; }
        public bool Positive { get; }
        public bool ShouldBeRendered { get; }

        public EffectRegistrationAttribute(string typeId, bool positive = true, bool shouldBeRendered = true)
        {
            TypeId = typeId;
            Positive = positive;
            ShouldBeRendered = shouldBeRendered;
        }
    }
}
