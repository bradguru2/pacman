using Silk.NET.Maths;

namespace PacMan
{
    public class FruitManager(Maze maze, Fruit fruit, double _spawnTime)
    {
        private readonly Maze _maze = maze;
        private readonly Fruit _fruit = fruit;        
        private bool _spawned = false;

        public Fruit TrySpawn(double currentTime)
        {
            if (!_spawned && currentTime - _spawnTime > 20.0) // every 20s
            {
                _fruit.SpawnTime = currentTime;
                _fruit.Active = true;
                _spawned = true;
            }

            if (_spawned && currentTime - _fruit.SpawnTime > Fruit.Lifetime)
            {
                _fruit.Active = false;
                _spawned = false;
                _spawnTime = currentTime; // reset spawn timer
            }

#pragma warning disable CS8603 // Possible null reference return.
            return _fruit;
#pragma warning restore CS8603 // Possible null reference return.
        }

        public bool TryEat(Vector2D<float> pacPos, double currentTime)
        {
            if (_fruit == null || !_fruit.Active) return false;
            float dx = pacPos.X - _fruit.PosUV.X;
            float dy = pacPos.Y - _fruit.PosUV.Y;
            if (dx * dx + dy * dy < 0.001f)
            {
                _fruit.Active = false;
                _spawned = false;
                _spawnTime = currentTime; // reset spawn timer
                return true;
            }
            return false;
        }
    }
}