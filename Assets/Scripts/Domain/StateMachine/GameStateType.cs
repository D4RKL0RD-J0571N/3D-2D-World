namespace Domain.Entities.StateMachine
{
    public enum GameStateType
    {
        None,
        MainMenu,
        Playing,
        Paused,
        GameOver,
        Loading
    }
}