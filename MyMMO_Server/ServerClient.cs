using System;
using System.IO;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Globalization;

namespace MyMMOServer
{
    public class ServerClient
    {
        public int Id { get; private set; }
        public string Username { get; private set; } = "Anonimo";
        public string CharacterClass { get; private set; } = "Guerrero";

        // --- ESTADÍSTICAS Y POSICIÓN INICIAL ---
        public int Level { get; set; } = 1;
        public int CurrentExp { get; private set; } = 0;
        public int MaxExp { get; private set; } = 1000;
        public int CurrentHp { get; set; } = 100;
        public int MaxHp { get; set; } = 100;
        public int AttributePoints { get; private set; } = 0;
        public int Strength { get; private set; } = 1;
        public int Precision { get; private set; } = 1;
        public int Agility { get; private set; } = 1;
        public int Vitality { get; private set; } = 1;
        public int BronzeCoins { get; private set; } = 0;
        public const int AttributeResetCost = 100;
        public float PosX { get; set; } = 0f;
        public float PosY { get; set; } = 1f;
        public float PosZ { get; set; } = 0f;
        public float RotY { get; set; } = 0f;
        public Inventory Inventory { get; } = new Inventory();
        public Equipment Equipment { get; } = new Equipment();

        private TcpClient socket;
        private NetworkStream stream;

        public ServerClient(TcpClient socket, int id)
        {
            this.socket = socket;
            this.Id = id;
            this.stream = socket.GetStream();
        }

        public void StartListening()
        {
            Thread t = new Thread(ListenToClient);
            t.IsBackground = true;
            t.Start();
        }

        private void ListenToClient()
        {
            try
            {
                using (StreamReader reader = new StreamReader(stream, Encoding.ASCII))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line == "QUIT")
                        {
                            break;
                        }

                        if (line.StartsWith("SEND_ATTACK:"))
                        {
                            string[] attackParts = line.Substring("SEND_ATTACK:".Length).Split(',');
                            if (attackParts.Length == 3)
                            {
                                Program.ProcessAttack(this.Id, attackParts[0], attackParts[1], attackParts[2]);
                            }
                            continue;
                        }

                        if (line.StartsWith("CAST_SKILL:"))
                        {
                            string[] commandParts = line.Split(':');
                            if (commandParts.Length == 3)
                            {
                                Program.ProcessCastSkill(this.Id, commandParts[1], commandParts[2]);
                            }
                            continue;
                        }

                        if (line.StartsWith("INVENTORY_MOVE:"))
                        {
                            string[] moveParts = line.Substring("INVENTORY_MOVE:".Length).Split(',');
                            if (moveParts.Length == 2 && int.TryParse(moveParts[0], out int sourceSlot) &&
                                int.TryParse(moveParts[1], out int destinationSlot) &&
                                Inventory.MoveOrSwap(sourceSlot, destinationSlot))
                            {
                                SendMessage($"INVENTORY_STATE:{Inventory.Serialize()}");
                            }

                            continue;
                        }

                        if (line.StartsWith("EQUIP_ITEM:"))
                        {
                            string[] equipParts = line.Substring("EQUIP_ITEM:".Length).Split(',');
                            if (equipParts.Length == 2 && int.TryParse(equipParts[0], out int inventoryIndex) &&
                                Equipment.TryEquip(Inventory, inventoryIndex, equipParts[1], CharacterClass))
                            {
                                SendMessage($"INVENTORY_STATE:{Inventory.Serialize()}");
                                SendMessage($"EQUIPMENT_STATE:{Equipment.Serialize()}");
                            }
                            else
                            {
                                int requestedIndex = -1;
                                string requestedItem = equipParts.Length > 0 && int.TryParse(equipParts[0], out requestedIndex)
                                    ? Inventory.GetItemId(requestedIndex)
                                    : "";
                                string requestedSlot = equipParts.Length > 1 ? equipParts[1] : "";
                                SendMessage($"EQUIP_FAILED:Objeto '{requestedItem}' en indice {requestedIndex} no compatible con slot '{requestedSlot}'.");
                            }

                            continue;
                        }

                        if (line.StartsWith("UNEQUIP_ITEM:"))
                        {
                            string slot = line.Substring("UNEQUIP_ITEM:".Length).Trim();
                            if (Equipment.TryUnequip(Inventory, slot))
                            {
                                SendMessage($"INVENTORY_STATE:{Inventory.Serialize()}");
                                SendMessage($"EQUIPMENT_STATE:{Equipment.Serialize()}");
                            }

                            continue;
                        }

                        if (line.StartsWith("SPEND_ATTRIBUTE:"))
                        {
                            string attribute = line.Substring("SPEND_ATTRIBUTE:".Length).Trim();
                            if (TrySpendAttribute(attribute)) SendProgress(0);
                            else SendMessage("ATTRIBUTE_FAILED:No tienes puntos o el atributo no es valido.");
                            continue;
                        }

                        if (line == "RESET_ATTRIBUTES")
                        {
                            if (TryResetAttributes()) SendProgress(0);
                            else SendMessage($"ATTRIBUTE_RESET_FAILED:Reiniciar atributos cuesta {AttributeResetCost} monedas de bronce.");
                            continue;
                        }

                        if (line.StartsWith("USE_ITEM:"))
                        {
                            string itemId = line.Substring("USE_ITEM:".Length).Trim();
                            if (Inventory.TryConsume(itemId, out int restoredAmount))
                            {
                                CurrentHp = Math.Min(MaxHp, CurrentHp + restoredAmount);
                                SendProgress(0);
                                SendMessage($"INVENTORY_STATE:{Inventory.Serialize()}");
                            }

                            continue;
                        }

                        // 1. PROCESAR LOGIN
                        if (line.StartsWith("LOGIN:"))
                        {
                            string rawData = line.Replace("LOGIN:", "");
                            string[] parts = rawData.Split(',');

                            if (parts.Length >= 2)
                            {
                                this.Username = parts[0];
                                this.CharacterClass = parts[1];
                            }

                            Console.WriteLine($"[LOGIN EXITOSO] ID {Id} -> Jugador: {Username} | Clase: {CharacterClass}");

                            // A) Responder al cliente recién conectado con sus datos de login
                            SendMessage($"LOGIN_SUCCESS:{Id},{PosX.ToString(CultureInfo.InvariantCulture)},{PosY.ToString(CultureInfo.InvariantCulture)},{PosZ.ToString(CultureInfo.InvariantCulture)},{RotY.ToString(CultureInfo.InvariantCulture)},{Level},{CurrentHp},{MaxHp},{CurrentExp},{MaxExp},{AttributePoints},{Strength},{Precision},{Agility},{Vitality},{BronzeCoins}");
                            SendMessage($"INVENTORY_STATE:{Inventory.Serialize()}");
                            SendMessage($"EQUIPMENT_STATE:{Equipment.Serialize()}");

                            // B) PRIMERO: Enviar al nuevo cliente la lista de jugadores que YA están conectados con sus nombres reales
                            Program.SendExistingPlayersTo(this);

                            // ¡AQUÍ AVISAMOS DE LOS ENEMIGOS!
                            Program.SendExistingEnemiesTo(this);

                            // C) DESPUÉS: Notificar a los demás que este nuevo jugador ha aparecido
                            string spawnMsg = $"SPAWN_PLAYER:{Id},{Username},{CharacterClass},{PosX.ToString(CultureInfo.InvariantCulture)},{PosY.ToString(CultureInfo.InvariantCulture)},{PosZ.ToString(CultureInfo.InvariantCulture)},{RotY.ToString(CultureInfo.InvariantCulture)}";
                            Program.BroadcastMessage(spawnMsg, this.Id);

                            continue;
                        }

                        // 2. PROCESAR POSICIÓN SIMPLE (POS:x,y,z)
                        if (line.StartsWith("POS:"))
                        {
                            string moveData = line.Replace("POS:", "");
                            UpdatePositionFromPacket(moveData);
                            Program.BroadcastMessage($"{Id}:POS:{moveData}", this.Id);
                            continue;
                        }

                        // 3. PROCESAR MOVIMIENTO Y ANIMACIONES (MOVE:x,y,z...)
                        if (line.StartsWith("MOVE:"))
                        {
                            string moveData = line.Replace("MOVE:", "");
                            UpdatePositionFromPacket(moveData);
                            string broadcastMove = $"MOVE_UPDATE:{Id},{moveData}";
                            Program.BroadcastMessage(broadcastMove, this.Id);
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR CLIENTE {Id}]: {ex.Message}");
            }
            finally
            {
                // Corregido: DISCONNECT en lugar de DESCONNECT
                Program.BroadcastMessage($"DISCONNECT:{Id}", this.Id);
                Program.DisconnectClient(this);
            }
        }

        private void UpdatePositionFromPacket(string moveData)
        {
            string[] coords = moveData.Split(',');
            if (coords.Length < 3) return;

            if (float.TryParse(coords[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(coords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(coords[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                PosX = x;
                PosY = y;
                PosZ = z;
            }

            if (coords.Length >= 4 && float.TryParse(coords[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotY))
            {
                RotY = rotY;
            }
        }

        public void SendMessage(string message)
        {
            try
            {
                byte[] buffer = Encoding.ASCII.GetBytes(message + "\n");
                stream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception) { }
        }

        public bool AddExperience(int amount)
        {
            if (amount <= 0) return false;

            bool leveledUp = false;
            CurrentExp += amount;
            while (CurrentExp >= MaxExp)
            {
                CurrentExp -= MaxExp;
                Level++;
                AttributePoints++;
                MaxExp = Level * 1000;
                MaxHp += 20;
                CurrentHp = MaxHp;
                leveledUp = true;
            }

            return leveledUp;
        }

        public bool TrySpendAttribute(string attribute)
        {
            if (AttributePoints <= 0) return false;

            switch (attribute)
            {
                case "strength": Strength++; break;
                case "precision": Precision++; break;
                case "agility": Agility++; break;
                case "vitality":
                    Vitality++;
                    MaxHp += 10;
                    CurrentHp += 10;
                    break;
                default: return false;
            }

            AttributePoints--;
            return true;
        }

        public bool TryResetAttributes()
        {
            if (BronzeCoins < AttributeResetCost) return false;

            AttributePoints += Strength - 1;
            AttributePoints += Precision - 1;
            AttributePoints += Agility - 1;
            AttributePoints += Vitality - 1;
            Strength = 1;
            Precision = 1;
            Agility = 1;
            Vitality = 1;
            MaxHp = Math.Max(100, 100 + (Level - 1) * 20);
            CurrentHp = Math.Min(CurrentHp, MaxHp);
            BronzeCoins -= AttributeResetCost;
            return true;
        }

        public void SendProgress(int experienceReward)
        {
            SendMessage($"PLAYER_PROGRESS:{Id},{Level},{CurrentExp},{MaxExp},{CurrentHp},{MaxHp},{experienceReward},{AttributePoints},{Strength},{Precision},{Agility},{Vitality},{BronzeCoins}");
        }

        public void AddBronzeCoins(int amount)
        {
            if (amount > 0) BronzeCoins += amount;
        }

        public int GetPhysicalDamage(int baseDamage)
        {
            int weaponBonus = Equipment.WeaponItemId switch
            {
                "iron_sword" => 5,
                "novice_bow" => 10,
                _ => 0
            };
            return Math.Max(1, baseDamage + (Strength - 1) + weaponBonus);
        }

        public int GetReceivedDamage(int baseDamage)
        {
            int armorReduction = Equipment.ArmorItemId == "leather_armor" ? 3 : 0;
            return Math.Max(1, baseDamage - armorReduction);
        }

        public void Close()
        {
            try
            {
                stream?.Close();
                socket?.Close();
            }
            catch (Exception) { }
        }
    }
    public sealed class EnemyProfile
    {
        public int MaxHp { get; private set; }
        public int Level { get; private set; }
        public float AggroRadius { get; private set; }
        public float LeashRadius { get; private set; }
        public float AttackRange { get; private set; }
        public float MoveSpeed { get; private set; }
        public float AttackDamage { get; private set; }
        public float AttackCooldown { get; private set; }
        public bool AggroOnSight { get; private set; }
        public int ExperienceReward { get; private set; }
        public int MinBronzeReward { get; private set; }
        public int MaxBronzeReward { get; private set; }
        public IReadOnlyList<EnemyLootDrop> LootTable { get; private set; }

        private EnemyProfile(int maxHp, int level, float aggroRadius, float leashRadius,
            float attackRange, float moveSpeed, float attackDamage, float attackCooldown,
            bool aggroOnSight, int experienceReward, int minBronzeReward, int maxBronzeReward,
            IReadOnlyList<EnemyLootDrop> lootTable)
        {
            MaxHp = maxHp;
            Level = level;
            AggroRadius = aggroRadius;
            LeashRadius = leashRadius;
            AttackRange = attackRange;
            MoveSpeed = moveSpeed;
            AttackDamage = attackDamage;
            AttackCooldown = attackCooldown;
            AggroOnSight = aggroOnSight;
            ExperienceReward = experienceReward;
            MinBronzeReward = minBronzeReward;
            MaxBronzeReward = maxBronzeReward;
            LootTable = lootTable;
        }

        public static EnemyProfile ForType(int type)
        {
            return type switch
            {
                2 => new EnemyProfile(100, 5, 12f, 30f, 2.5f, 1.8f, 15f, 1.8f, true, 500, 25, 60,
                    new[]
                    {
                        new EnemyLootDrop("armor_scraps", 0.45),
                        new EnemyLootDrop("broken_sword", 0.30)
                    }),
                3 => new EnemyProfile(35, 2, 14f, 32f, 1.8f, 4.2f, 8f, 1.0f, false, 175, 8, 20,
                    new[]
                    {
                        new EnemyLootDrop("wolf_fang", 0.50),
                        new EnemyLootDrop("wolf_pelt", 0.35),
                        new EnemyLootDrop("raw_meat", 0.60)
                    }),
                _ => new EnemyProfile(50, 1, 10f, 25f, 2.2f, 2.3f, 10f, 1.4f, true, 100, 5, 15,
                    new[]
                    {
                        new EnemyLootDrop("armor_scraps", 0.35),
                        new EnemyLootDrop("broken_sword", 0.20)
                    })
            };
        }
    }

    public sealed class EnemyLootDrop
    {
        public string ItemId { get; }
        public double Chance { get; }

        public EnemyLootDrop(string itemId, double chance)
        {
            ItemId = itemId;
            Chance = chance;
        }
    }

    public enum EnemyState
    {
        Wander = 0,
        Chase = 1,
        Attack = 2
    }

    public class Enemy
    {
        public int Id { get; set; }
        public int EnemyType { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public int Level { get; set; } = 1;
        public EnemyState State { get; set; } = EnemyState.Wander;
        public int TargetClientId { get; set; } = 0;
        public float AggroRadius { get; set; } = 10f;
        public float LeashRadius { get; set; } = 25f;
        public float AttackRange { get; set; } = 2.2f;
        public float MoveSpeed { get; set; } = 2.3f;
        public float AttackDamage { get; set; } = 10f;
        public float AttackCooldown { get; set; } = 1.4f;
        public bool AggroOnSight { get; set; } = true;
        public float AttackTimer { get; set; } = 0f;
        public Vector3 WanderTarget { get; set; }
        public Vector3 SpawnPosition { get; set; }
        public ZoneSpawner? Spawner { get; set; }

        public Enemy(int id, int type, float x, float y, float z, int maxHp, ZoneSpawner? spawner = null)
        {
            Id = id;
            EnemyType = type;
            PosX = x;
            PosY = y;
            PosZ = z;
            SpawnPosition = new Vector3(x, y, z);
            WanderTarget = new Vector3(x, y, z);
            MaxHp = maxHp;
            CurrentHp = maxHp;
            Spawner = spawner;
            State = EnemyState.Wander;

            EnemyProfile profile = EnemyProfile.ForType(type);
            AggroRadius = profile.AggroRadius;
            LeashRadius = profile.LeashRadius;
            AttackRange = profile.AttackRange;
            MoveSpeed = profile.MoveSpeed;
            AttackDamage = profile.AttackDamage;
            AttackCooldown = profile.AttackCooldown;
            AggroOnSight = profile.AggroOnSight;
        }
    }
}