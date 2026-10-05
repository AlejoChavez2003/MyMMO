using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Linq;

namespace MyMMOServer
{
    class Program
    {
        private const float EnemyAiTickSeconds = 0.2f;
        private const float AttackStoppingDistance = 2f;
        private const float PlayerRespawnDelaySeconds = 3f;
        private const bool AiDebugLoggingEnabled = false;

        private static readonly List<ServerClient> clients = new();
        private static readonly object clientsLock = new();
        private static readonly object enemiesLock = new();
        private static int nextId = 1;
        public static readonly Dictionary<int, Enemy> enemies = new();
        private static int nextEnemyId = 1;
        private static readonly List<ZoneSpawner> zoneSpawners = new();

        public static void SpawnInitialEnemies()
        {
            Console.WriteLine("[SERVIDOR] Creando enemigos iniciales en el mapa...");

            zoneSpawners.Add(new ZoneSpawner(4, 10f, new Vector3(0f, 0f, 0f), 1, 10f));
            zoneSpawners.Add(new ZoneSpawner(1, 2f, new Vector3(20f, 0f, 20f), 2, 60f));
            zoneSpawners.Add(new ZoneSpawner(3, 8f, new Vector3(-20f, 0f, 15f), 3, 20f));

            foreach (ZoneSpawner spawner in zoneSpawners)
            {
                spawner.Start();
            }
        }

        public static Enemy SpawnEnemy(int type, float x, float y, float z, ZoneSpawner spawner)
        {
            int id;
            EnemyProfile profile = EnemyProfile.ForType(type);
            lock (enemiesLock)
            {
                id = nextEnemyId++;
                Enemy newEnemy = new Enemy(id, type, x, y, z, profile.MaxHp, spawner)
                {
                    Level = profile.Level,
                    AggroRadius = profile.AggroRadius,
                    LeashRadius = profile.LeashRadius,
                    AttackRange = profile.AttackRange,
                    MoveSpeed = profile.MoveSpeed,
                    AttackDamage = profile.AttackDamage,
                    AttackCooldown = profile.AttackCooldown
                };
                ResetEnemyForRespawn(newEnemy);
                enemies.Add(id, newEnemy);

                string spawnMsg = $"SPAWN_ENEMY:{id},{type},{profile.MaxHp},{profile.Level},{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)},{z.ToString(CultureInfo.InvariantCulture)}";
                BroadcastMessage(spawnMsg, 0);
                return newEnemy;
            }
        }

        private static void ResetEnemyForRespawn(Enemy enemy)
        {
            ClearTargetAndWander(enemy);
            enemy.AttackTimer = 0f;
            enemy.WanderTarget = enemy.SpawnPosition;
        }

        public static void ProcessBasicAttack(int attackerId, string targetIdValue)
        {
            if (int.TryParse(targetIdValue, out int targetId))
            {
                ServerClient? attacker = FindClientById(attackerId);
                ApplyDamageToEnemy(targetId, attacker?.GetPhysicalDamage(5) ?? 5, attackerId, "BASIC_ATTACK");
            }
        }

        public static void ProcessAttack(int attackerId, string abilityId, string targetIdValue, string slotValue)
        {
            if (!int.TryParse(targetIdValue, out int targetId) || !int.TryParse(slotValue, out int slot)) return;

            ServerClient? attacker = FindClientById(attackerId);
            if (attacker == null || !IsAbilityAllowed(attacker, abilityId))
            {
                Console.WriteLine($"[COMBATE] Habilidad rechazada: jugador {attackerId}, habilidad {abilityId}.");
                return;
            }

            int damage = attacker.GetPhysicalDamage(abilityId == "BASIC_ATTACK" ? 5 : 15);
            ApplyDamageToEnemy(targetId, damage, attackerId, abilityId, slot);
        }

        private static bool IsAbilityAllowed(ServerClient client, string abilityId)
        {
            if (abilityId == "BASIC_ATTACK" || abilityId == "1001") return true;
            return abilityId == "1002" && client.Level >= 3 && client.CharacterClass == "Guerrero";
        }

        public static void ProcessCastSkill(int attackerId, string skillId, string targetIdValue)
        {
            ServerClient? attacker = FindClientById(attackerId);
            if (attacker == null || !IsAbilityAllowed(attacker, skillId))
            {
                Console.WriteLine($"[COMBATE] Habilidad rechazada: jugador {attackerId}, habilidad {skillId}.");
                return;
            }

            if (int.TryParse(targetIdValue, out int targetId))
            {
                Console.WriteLine($"[COMBATE] Habilidad {skillId} contra enemigo {targetId}.");
                ApplyDamageToEnemy(targetId, attacker.GetPhysicalDamage(skillId == "BASIC_ATTACK" ? 5 : 15), attackerId, skillId, 0);
            }
        }

        private static void ApplyDamageToEnemy(int targetId, int damage, int attackerId = 0, string abilityId = "", int slot = -1)
        {
            int currentHp;
            bool died = false;
            int experienceReward = 0;
            int bronzeReward = 0;
            string dropItemId = "";
            ZoneSpawner? spawner = null;
            string? chaseStatePacket = null;

            lock (enemiesLock)
            {
                if (!enemies.TryGetValue(targetId, out Enemy enemy))
                {
                    Console.WriteLine($"[COMBATE] Enemigo {targetId} no encontrado.");
                    return;
                }

                if (attackerId > 0)
                {
                    enemy.TargetClientId = attackerId;
                    enemy.State = EnemyState.Chase;
                    enemy.AttackTimer = 0f;
                    chaseStatePacket = FormatEnemyState(enemy);
                }

                enemy.CurrentHp = Math.Max(0, enemy.CurrentHp - damage);
                currentHp = enemy.CurrentHp;
                if (currentHp <= 0)
                {
                    enemies.Remove(targetId);
                    spawner = enemy.Spawner;
                    EnemyProfile profile = EnemyProfile.ForType(enemy.EnemyType);
                    experienceReward = profile.ExperienceReward;
                    bronzeReward = Random.Shared.Next(profile.MinBronzeReward, profile.MaxBronzeReward + 1);
                    if (Random.Shared.NextDouble() <= profile.DropChance)
                    {
                        dropItemId = profile.DropItemId;
                    }
                    died = true;
                }
            }

            if (chaseStatePacket != null)
            {
                BroadcastMessage(chaseStatePacket);
            }

            if (attackerId > 0)
            {
                BroadcastMessage($"RECEIVE_ATTACK_BROADCAST:{attackerId},{abilityId},{targetId},{slot}", attackerId);
            }

            BroadcastMessage($"UPDATE_ENEMY_HP:{targetId},{currentHp}");
            if (died)
            {
                BroadcastMessage($"ENEMY_DIED:{targetId}");
                ServerClient? attacker = FindClientById(attackerId);
                if (attacker != null)
                {
                    attacker.AddExperience(experienceReward);
                    attacker.AddBronzeCoins(bronzeReward);
                    if (!string.IsNullOrEmpty(dropItemId))
                    {
                        if (attacker.Inventory.AddItem(dropItemId, 1))
                        {
                            attacker.SendMessage($"DROP_REWARD:{dropItemId},1");
                            attacker.SendMessage($"INVENTORY_STATE:{attacker.Inventory.Serialize()}");
                        }
                        else
                        {
                            attacker.SendMessage($"DROP_FAILED:{dropItemId},INVENTORY_FULL");
                        }
                    }
                    attacker.SendProgress(experienceReward);
                }
                spawner?.NotifyEnemyDied(targetId);
            }
        }


        private static void UpdateEnemiesAi()
        {
            List<ServerClient> playersSnapshot;
            lock (clientsLock)
            {
                playersSnapshot = new List<ServerClient>(clients);
            }

            List<string> outgoing = new();
            lock (enemiesLock)
            {
                foreach (Enemy enemy in enemies.Values)
                {
                    UpdateEnemyState(enemy, playersSnapshot, outgoing);
                }
            }

            foreach (string message in outgoing)
            {
                BroadcastMessage(message);
            }
        }

        private static void UpdateEnemyState(Enemy enemy, List<ServerClient> players, List<string> outgoing)
        {
            enemy.AttackTimer = Math.Max(0f, enemy.AttackTimer - EnemyAiTickSeconds);

            List<ServerClient> alivePlayers = players.Where(p => p.CurrentHp > 0).ToList();

            ServerClient? closestPlayer = null;
            float closestDistance = float.MaxValue;
            foreach (ServerClient player in alivePlayers)
            {
                float playerDistance = GetDistanceToPosition(enemy.PosX, enemy.PosZ, player.PosX, player.PosZ);
                if (playerDistance < closestDistance)
                {
                    closestDistance = playerDistance;
                    closestPlayer = player;
                }
            }

            ServerClient? target = null;
            if (enemy.TargetClientId > 0)
            {
                target = alivePlayers.FirstOrDefault(p => p.Id == enemy.TargetClientId);
            }

            if (target != null)
            {
                float targetDistance = GetDistanceToPosition(enemy.PosX, enemy.PosZ, target.PosX, target.PosZ);
                if (targetDistance > enemy.LeashRadius && targetDistance > enemy.AggroRadius)
                {
                    ClearTargetAndWander(enemy);
                    target = null;
                }
            }
            else
            {
                enemy.TargetClientId = 0;
            }

            if (enemy.TargetClientId <= 0 && closestPlayer != null && closestDistance <= enemy.AggroRadius)
            {
                enemy.TargetClientId = closestPlayer.Id;
                enemy.State = EnemyState.Chase;
                target = closestPlayer;
            }

            if (enemy.TargetClientId <= 0 || target == null)
            {
                enemy.State = EnemyState.Wander;
                StepWander(enemy);
                QueueEnemyState(enemy, outgoing);
                return;
            }

            float distance = GetDistanceToPosition(enemy.PosX, enemy.PosZ, target.PosX, target.PosZ);
            float stopDistance = Math.Max(AttackStoppingDistance, enemy.AttackRange);
            if (distance <= stopDistance)
            {
                enemy.State = EnemyState.Attack;
                QueueEnemyState(enemy, outgoing);
                TryHitTarget(enemy, target, outgoing);
            }
            else
            {
                enemy.State = EnemyState.Chase;
                MoveToward(enemy, target.PosX, target.PosZ, enemy.MoveSpeed * EnemyAiTickSeconds, stopDistance);
                QueueEnemyState(enemy, outgoing);
            }
        }

        private static void ClearTargetAndWander(Enemy enemy)
        {
            enemy.State = EnemyState.Wander;
            enemy.TargetClientId = 0;
            enemy.WanderTarget = enemy.SpawnPosition;
        }

        private static void StepWander(Enemy enemy)
        {
            if (GetDistanceToPosition(enemy.PosX, enemy.PosZ, enemy.WanderTarget.X, enemy.WanderTarget.Z) <= 0.5f)
            {
                enemy.WanderTarget = GetRandomWanderTarget(enemy);
            }

            MoveToward(enemy, enemy.WanderTarget.X, enemy.WanderTarget.Z, (enemy.MoveSpeed * 0.6f) * EnemyAiTickSeconds);
        }

        private static void TryHitTarget(Enemy enemy, ServerClient target, List<string> outgoing)
        {
            if (enemy.AttackTimer > 0f) return;

            int damage = target.GetReceivedDamage((int)enemy.AttackDamage);
            target.CurrentHp = Math.Max(0, target.CurrentHp - damage);
            enemy.AttackTimer = enemy.AttackCooldown;

            outgoing.Add($"ENEMY_ATTACK:{enemy.Id},{target.Id},{damage}");
            outgoing.Add($"PLAYER_HP:{target.Id},{target.CurrentHp}");
            if (target.CurrentHp <= 0)
            {
                outgoing.Add($"PLAYER_DEFEATED:{target.Id}");
                _ = RespawnPlayerAfterDelayAsync(target);
            }
        }

        public static async Task RespawnPlayerAfterDelayAsync(ServerClient target)
        {
            await Task.Delay(TimeSpan.FromSeconds(PlayerRespawnDelaySeconds));

            if (target == null) return;

            target.CurrentHp = target.MaxHp;
            target.PosX = 0f;
            target.PosY = 1f;
            target.PosZ = 0f;
            target.RotY = 0f;

            BroadcastMessage($"PLAYER_HP:{target.Id},{target.CurrentHp}");
            BroadcastMessage($"PLAYER_RESPAWN:{target.Id},{target.PosX.ToString(CultureInfo.InvariantCulture)},{target.PosY.ToString(CultureInfo.InvariantCulture)},{target.PosZ.ToString(CultureInfo.InvariantCulture)},{target.RotY.ToString(CultureInfo.InvariantCulture)}");
        }

        private static void QueueEnemyState(Enemy enemy, List<string> outgoing)
        {
            outgoing.Add(FormatEnemyState(enemy));
        }

        private static string FormatEnemyState(Enemy enemy)
        {
            return $"ENEMY_STATE:{enemy.Id},{(int)enemy.State},{enemy.TargetClientId},{enemy.PosX.ToString(CultureInfo.InvariantCulture)},{enemy.PosY.ToString(CultureInfo.InvariantCulture)},{enemy.PosZ.ToString(CultureInfo.InvariantCulture)}";
        }

        private static Vector3 GetRandomWanderTarget(Enemy enemy)
        {
            float radius = Math.Max(3f, enemy.AggroRadius * 0.5f);
            double angle = Random.Shared.NextDouble() * Math.PI * 2.0;
            double distance = Random.Shared.NextDouble() * radius;

            float x = enemy.SpawnPosition.X + (float)(Math.Cos(angle) * distance);
            float z = enemy.SpawnPosition.Z + (float)(Math.Sin(angle) * distance);

            return new Vector3(x, enemy.SpawnPosition.Y, z);
        }

        private static void MoveToward(Enemy enemy, float targetX, float targetZ, float step, float stopDistance = 0f)
        {
            float dx = targetX - enemy.PosX;
            float dz = targetZ - enemy.PosZ;
            float magnitude = (float)Math.Sqrt(dx * dx + dz * dz);
            if (magnitude <= 0.0001f) return;

            float remaining = magnitude - stopDistance;
            if (remaining <= 0.15f)
            {
                return;
            }

            float moveLength = Math.Min(remaining, step);
            enemy.PosX += (dx / magnitude) * moveLength;
            enemy.PosZ += (dz / magnitude) * moveLength;
        }

        private static float GetDistanceToPosition(float startX, float startZ, float targetX, float targetZ)
        {
            float dx = targetX - startX;
            float dz = targetZ - startZ;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static void SendExistingEnemiesTo(ServerClient client)
        {
            Console.WriteLine($"[SERVIDOR] Intentando enviar {enemies.Count} enemigos a {client.Username}...");

            foreach (var enemy in enemies.Values)
            {
                string msg = $"SPAWN_ENEMY:{enemy.Id},{enemy.EnemyType},{enemy.MaxHp},{enemy.Level},{enemy.PosX.ToString(CultureInfo.InvariantCulture)},{enemy.PosY.ToString(CultureInfo.InvariantCulture)},{enemy.PosZ.ToString(CultureInfo.InvariantCulture)}";
                client.SendMessage(msg);
            }
        }

        static void Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.Any, 7777);
            server.Start();
            Console.WriteLine("[SERVIDOR] Iniciado en puerto 7777. Esperando jugadores...");

            SpawnInitialEnemies();

            Thread aiThread = new Thread(EnemyAiLoop)
            {
                IsBackground = true
            };
            aiThread.Start();

            while (true)
            {
                TcpClient tcpClient = server.AcceptTcpClient();

                ServerClient newClient;
                lock (clientsLock)
                {
                    newClient = new ServerClient(tcpClient, nextId);
                    clients.Add(newClient);
                    nextId++;
                }

                Console.WriteLine($"[CONEXIÓN] Cliente conectado (ID: {newClient.Id})");
                newClient.StartListening();
            }
        }

        private static void EnemyAiLoop()
        {
            while (true)
            {
                Thread.Sleep((int)(EnemyAiTickSeconds * 1000f));
                UpdateEnemiesAi();
            }
        }

        public static void BroadcastMessage(string message, int ignoreId = -1)
        {
            lock (clientsLock)
            {
                foreach (var client in clients)
                {
                    if (client.Id != ignoreId)
                    {
                        client.SendMessage(message);
                    }
                }
            }
        }

        private static ServerClient? FindClientById(int clientId)
        {
            if (clientId <= 0) return null;

            lock (clientsLock)
            {
                return clients.FirstOrDefault(client => client.Id == clientId);
            }
        }

        public static void SendExistingPlayersTo(ServerClient newClient)
        {
            lock (clientsLock)
            {
                foreach (var client in clients)
                {
                    if (client.Id != newClient.Id)
                    {
                        string existingPlayerMsg = $"SPAWN_PLAYER:{client.Id},{client.Username},{client.CharacterClass},{client.PosX},{client.PosY},{client.PosZ},{client.RotY}";
                        newClient.SendMessage(existingPlayerMsg);
                    }
                }
            }
        }

        public static void DisconnectClient(ServerClient client)
        {
            lock (clientsLock)
            {
                if (clients.Contains(client))
                {
                    clients.Remove(client);
                    client.Close();

                    Console.WriteLine($"[SERVIDOR] El cliente {client.Id} ({client.Username}) se ha desconectado.");

                    string disconnectMessage = $"DISCONNECT:{client.Id}";

                    foreach (var c in clients)
                    {
                        c.SendMessage(disconnectMessage);
                    }
                }
            }
        }
    }
}