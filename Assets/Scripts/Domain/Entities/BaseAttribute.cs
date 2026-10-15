namespace Domain.Entities
{
    using System;


    //TODO: Convert this into a scriptable object to save already 
    // made attributes to share with other actors through the AttributeComponent
    [Serializable]
    public class BaseAttribute
    {
        public string AttributeName;
        public int MaximumValue;
        public int CurrentValue;
        public float RegenerationRate;
        public float RegenerationMultiplier = 1.0f;
        public float CombatRegenerationRate;

        public BaseAttribute Clone()
        {
            return new BaseAttribute
            {
                AttributeName = this.AttributeName,
                MaximumValue = this.MaximumValue,
                CurrentValue = this.MaximumValue, // Initialize at max
                RegenerationRate = this.RegenerationRate,
                RegenerationMultiplier = this.RegenerationMultiplier,
                CombatRegenerationRate = this.CombatRegenerationRate
            };
        }
    }
}
