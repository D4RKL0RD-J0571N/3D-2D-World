namespace Domain.LevelGeneration
{
    public interface IRoomGraphGenerator
    {
        LevelLayout Generate(LevelGenerationParameters parameters);
    }
}
