namespace Vesolovsky.Core.Utils
{
    public static class DirectionUtil
    {
        //TODO: add to the core
        public static Direction Opposite(this Direction direction) =>
            direction switch
            {
                Direction.Up => Direction.Down,
                Direction.Down => Direction.Up,
                Direction.Left => Direction.Right,
                _ => Direction.Left
            };
    }
}
