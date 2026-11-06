namespace Domain.Museum
{
    /// <summary>
    /// Categorization taxonomy for museum exhibits and test showcases.
    /// </summary>
    public enum ExhibitCategory
    {
        Weapon,
        Enemy,
        LootItem,
        VisualFX,
        ShaderEffect,
        EnvironmentBiome,
        Mechanic
    }

    /// <summary>
    /// Evaluation rating assigned to an asset or mechanic by the developer/curator.
    /// </summary>
    public enum CuratorRating
    {
        Unrated = 0,
        Keep = 1,
        Refine = 2,
        Discard = 3
    }
}
