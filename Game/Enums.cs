// /Game/Enums.cs
namespace Snake2000.Gameplay
{
    // GameState vit dans Game/Gameplay/GameState.cs et GameMode dans
    // Game/Gameplay/GameMode.cs. Les deux etaient egalement declares ici, a
    // l'identique et dans le meme espace de noms : CS0101, le projet ne pouvait
    // pas compiler. Le fichier dedie fait foi.
    public enum Direction { Up, Down, Left, Right }
    public enum SpecialKind { None, Growth, Shrink, SpeedUp, SlowDown, Split, Rejoin, Ghost, Invincible, Poison, Bonus, TimeWarp, Chaos, Fusion, Invisibility, Shield, Teleport, Mirror, Reverse, Confusion, Freeze, Random, Portal, Magnet, Bomb, Heal, ScoreX2, Life, ExtraTime, ColorChange, ShapeShift, GravityFlip, WallWalk, Phase, Echo, Clone, Swap, SwapControl, SwapSnake, SwapApple, SwapObstacle, SwapBackground, SwapMusic, SwapSfx, SwapTheme, SwapColor, SwapShape, SwapMode, SwapLevel, SwapPlayer, SwapSnakeColor, SwapSnakeShape, SwapBoardTheme, SwapGameSpeed, SwapAppleCount, SwapObstacleCount, SwapSpecialCount, SwapBoss, SwapModeSetting, SwapLevelSetting, SwapPlayerSetting, SwapSnakeColorSettingValue, SwapSnakeShapeSettingValue, SwapBoardThemeSettingValue, SwapGameSpeedSettingValue, SwapAppleCountSettingValue, SwapObstacleCountSettingValue, SwapSpecialCountSettingValue, SwapBossSettingValue }
}