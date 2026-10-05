using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace MyMMOServer
{
    public sealed class ZoneSpawner
    {
        public int maxEnemies;
        public float spawnRadius;
        public Vector3 centerPosition;
        public int enemyType;
        public float respawnDelay;

        private readonly object syncRoot = new object();
        private readonly HashSet<int> livingEnemyIds = new HashSet<int>();
        private readonly Random random = new Random();
        private int pendingRespawns;

        public ZoneSpawner(int maxEnemies, float spawnRadius, Vector3 centerPosition, int enemyType, float respawnDelay)
        {
            if (maxEnemies < 1) throw new ArgumentOutOfRangeException(nameof(maxEnemies));
            if (spawnRadius < 0) throw new ArgumentOutOfRangeException(nameof(spawnRadius));
            if (respawnDelay < 0) throw new ArgumentOutOfRangeException(nameof(respawnDelay));

            this.maxEnemies = maxEnemies;
            this.spawnRadius = spawnRadius;
            this.centerPosition = centerPosition;
            this.enemyType = enemyType;
            this.respawnDelay = respawnDelay;
        }

        public void Start()
        {
            for (int index = 0; index < maxEnemies; index++)
            {
                SpawnOne();
            }
        }

        public void NotifyEnemyDied(int enemyId)
        {
            lock (syncRoot)
            {
                if (!livingEnemyIds.Remove(enemyId)) return;
                pendingRespawns++;
            }

            _ = RespawnAfterDelayAsync();
        }

        private async Task RespawnAfterDelayAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(respawnDelay));

            lock (syncRoot)
            {
                if (pendingRespawns == 0) return;
                pendingRespawns--;
            }

            SpawnOne();
        }

        private void SpawnOne()
        {
            Vector3 position = GetRandomPosition();
            Enemy enemy = Program.SpawnEnemy(enemyType, position.X, position.Y, position.Z, this);

            lock (syncRoot)
            {
                livingEnemyIds.Add(enemy.Id);
            }
        }

        private Vector3 GetRandomPosition()
        {
            double angle;
            double distance;
            lock (random)
            {
                angle = random.NextDouble() * Math.PI * 2;
                distance = Math.Sqrt(random.NextDouble()) * spawnRadius;
            }

            return new Vector3(
                centerPosition.X + (float)(Math.Cos(angle) * distance),
                centerPosition.Y,
                centerPosition.Z + (float)(Math.Sin(angle) * distance));
        }
    }
}