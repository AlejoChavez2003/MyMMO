using UnityEngine;
using System.Net.Sockets;
using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using TMPro;

public class NetworkManager : MonoBehaviour
{
    [Header("Configuración del Servidor")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 7777;

    [Header("Referencias de Jugadores")]
    public Transform localPlayer;
    public GameObject jugadorPrefab; 

    [Header("Ajuste de Altura Remota")]
    [Tooltip("Offset vertical para compensar la falta de CharacterController en el clon remoto")]
    public float remoteYOffset = 0.05f; 

    private TcpClient client;
    private NetworkStream stream;
    private StreamReader reader;

    private float sendInterval = 0.05f; // 20 envíos por segundo
    private float timer;
    private int myPlayerId = -1;
    private string messageBuffer = "";

    private Dictionary<int, string> playerNames = new Dictionary<int, string>();
    private Dictionary<int, GameObject> remotePlayers = new Dictionary<int, GameObject>();

    private Animator localAnimator;
    private CharacterController localController;

    [NonSerialized] public Dictionary<int, GameObject> activeEnemies = new Dictionary<int, GameObject>();
    public GameObject enemyPrefab; // Arrastra tu Prefab de enemigo aquí desde el inspector
    public GameObject wolfPrefab;
    public GameObject damageTextPrefab;

    public async Task ConnectToServerAsync()
    {
        try
        {
            client = new TcpClient();

            var connectTask = client.ConnectAsync(serverIP, serverPort);
            var timeoutTask = Task.Delay(2000);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == timeoutTask || !client.Connected)
            {
                Debug.LogWarning("[Red] Servidor no detectado.");
                client.Close();
                return;
            }

            stream = client.GetStream();
            reader = new StreamReader(stream, Encoding.ASCII);

            if (localPlayer != null)
            {
                localAnimator = localPlayer.GetComponentInChildren<Animator>();
                localController = localPlayer.GetComponent<CharacterController>();
            }

            string loginPacket = $"LOGIN:{GameData.PlayerUsername},{GameData.PlayerClass}\n";
            byte[] data = Encoding.ASCII.GetBytes(loginPacket);
            await stream.WriteAsync(data, 0, data.Length);

            Debug.Log($"[Red] ¡Conectado exitosamente como {GameData.PlayerUsername}!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Red] Error al conectar: {e.Message}");
        }
    }

    void Update()
    {
        if (client == null || !client.Connected || stream == null) return;

        // 1. LEER MENSAJES DEL SERVIDOR (Directo y sin trampas)
        try
        {
            if (stream.DataAvailable)
            {
                // Leemos todo lo que acaba de llegar por el cable
                byte[] readBuffer = new byte[4096];
                int bytesRead = stream.Read(readBuffer, 0, readBuffer.Length);
                string data = Encoding.ASCII.GetString(readBuffer, 0, bytesRead);
                
                // Lo guardamos en nuestro almacén
                messageBuffer += data;

                // Buscamos los saltos de línea (\n) para separar cada mensaje individual
                int newLineIndex;
                while ((newLineIndex = messageBuffer.IndexOf('\n')) >= 0)
                {
                    string line = messageBuffer.Substring(0, newLineIndex).Trim();
                    // Recortamos el mensaje que ya procesamos del almacén
                    messageBuffer = messageBuffer.Substring(newLineIndex + 1);

                    if (!string.IsNullOrEmpty(line))
                    {
                        ProcessMessage(line);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Red] Error al leer datos: {e.Message}");
        }

        // 2. ENVIAR ESTADO DEL JUGADOR LOCAL
        timer += Time.deltaTime;
        if (timer >= sendInterval)
        {
            SendLocalState();
            timer = 0f;
        }
    }

    private void SendLocalState()
    {
        Vector3 pos = localPlayer != null ? localPlayer.position : transform.position;
        float rotY = localPlayer != null ? localPlayer.eulerAngles.y : 0f;

        // En lugar de usar CharacterController, leemos directamente tu Animator local
        bool isMoving = localAnimator != null ? localAnimator.GetBool("isMoving") : false;
        bool isRunning = localAnimator != null ? localAnimator.GetBool("isRunning") : false;
        bool isFalling = localAnimator != null ? localAnimator.GetBool("isFalling") : false; 
        bool jumpTrigger = localAnimator != null ? localAnimator.GetBool("Jump") : false;

        string x = pos.x.ToString("F2", CultureInfo.InvariantCulture);
        string y = pos.y.ToString("F2", CultureInfo.InvariantCulture);
        string z = pos.z.ToString("F2", CultureInfo.InvariantCulture);
        string rot = rotY.ToString("F2", CultureInfo.InvariantCulture);

        string message = $"MOVE:{x},{y},{z},{rot},{isMoving},{isRunning},{isFalling},{jumpTrigger}\n";
        byte[] data = Encoding.ASCII.GetBytes(message);

        try
        {
            stream.Write(data, 0, data.Length);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Red] Error al enviar posición: {ex.Message}");
        }
    }

    public void SendBasicAttack(int targetId)
    {
        SendCommand($"BASIC_ATTACK:{targetId}");
    }

    public void SendCastSkill(string skillId, int targetId)
    {
        if (string.IsNullOrWhiteSpace(skillId)) return;
        SendCommand($"CAST_SKILL:{skillId}:{targetId}");
    }

    public void SendAttack(string abilityId, int targetId, int slot)
    {
        if (string.IsNullOrWhiteSpace(abilityId)) return;
        SendCommand($"SEND_ATTACK:{abilityId},{targetId},{slot}");
    }

    public void SendInventoryMove(int sourceSlot, int destinationSlot)
    {
        if (sourceSlot < 0 || sourceSlot >= InventoryData.SlotCount ||
            destinationSlot < 0 || destinationSlot >= InventoryData.SlotCount ||
            sourceSlot == destinationSlot) return;

        SendCommand($"INVENTORY_MOVE:{sourceSlot},{destinationSlot}");
    }

    public void SendUseItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        Debug.Log($"[Inventario] Solicitando usar {itemId}.");
        SendCommand($"USE_ITEM:{itemId}");
    }

    public void SendEquipItem(int inventorySlot, string equipmentSlot)
    {
        if (inventorySlot < 0 || inventorySlot >= InventoryData.SlotCount || string.IsNullOrWhiteSpace(equipmentSlot)) return;
        SendCommand($"EQUIP_ITEM:{inventorySlot},{equipmentSlot}");
    }

    public void SendUnequipItem(string equipmentSlot)
    {
        if (string.IsNullOrWhiteSpace(equipmentSlot)) return;
        SendCommand($"UNEQUIP_ITEM:{equipmentSlot}");
    }

    public void SendSpendAttribute(string attribute)
    {
        if (string.IsNullOrWhiteSpace(attribute)) return;
        SendCommand($"SPEND_ATTRIBUTE:{attribute}");
    }

    public void SendResetAttributes()
    {
        SendCommand("RESET_ATTRIBUTES");
    }

    private void SendCommand(string message)
    {
        Debug.Log($"[Red Enviado] -> {message}");

        if (stream == null || client == null || !client.Connected) return;

        try
        {
            byte[] data = Encoding.ASCII.GetBytes(message + "\n");
            stream.Write(data, 0, data.Length);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Red] Error al enviar comando: {ex.Message}");
        }
    }

    private static void ApplyAttributeData(string[] values, int startIndex)
    {
        if (values.Length <= startIndex + 4) return;
        if (int.TryParse(values[startIndex], out int points)) GameData.AttributePoints = Mathf.Max(0, points);
        if (int.TryParse(values[startIndex + 1], out int strength)) GameData.Strength = Mathf.Max(1, strength);
        if (int.TryParse(values[startIndex + 2], out int precision)) GameData.Precision = Mathf.Max(1, precision);
        if (int.TryParse(values[startIndex + 3], out int agility)) GameData.Agility = Mathf.Max(1, agility);
        if (int.TryParse(values[startIndex + 4], out int vitality)) GameData.Vitality = Mathf.Max(1, vitality);
    }

    private static void ApplyCurrencyData(string[] values, int index)
    {
        if (values.Length > index && int.TryParse(values[index], out int bronzeCoins))
        {
            GameData.BronzeCoins = Mathf.Max(0, bronzeCoins);
        }
    }

    void ProcessMessage(string line)
    {
        Debug.Log($"[Red Recibido] -> {line}");
        string[] parts = line.Split(':');

        if (parts[0] == "LOGIN_SUCCESS" && parts.Length > 1)
        {
            string[] loginData = parts[1].Split(',');
            if (int.TryParse(loginData[0], out int assignedId))
            {
                myPlayerId = assignedId;
                Debug.Log($"[Red] Mi ID asignado: {myPlayerId}");
            }

            if (loginData.Length >= 8)
            {
                if (float.TryParse(loginData[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float spawnX) &&
                    float.TryParse(loginData[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float spawnY) &&
                    float.TryParse(loginData[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float spawnZ))
                {
                    if (localPlayer != null)
                    {
                        localPlayer.position = new Vector3(spawnX, spawnY, spawnZ);
                    }
                }

                if (int.TryParse(loginData[6], out int currentHp))
                {
                    GameData.CurrentHP = Mathf.Clamp(currentHp, 0, GameData.MaxHP);
                }

                if (int.TryParse(loginData[7], out int maxHp))
                {
                    GameData.MaxHP = Mathf.Max(1, maxHp);
                    GameData.CurrentHP = Mathf.Clamp(GameData.CurrentHP, 0, GameData.MaxHP);
                }

                if (int.TryParse(loginData[5], out int level))
                {
                    GameData.Level = Mathf.Max(1, level);
                }

                if (loginData.Length >= 10)
                {
                    if (int.TryParse(loginData[8], out int currentExp))
                    {
                        GameData.CurrentEXP = Mathf.Max(0, currentExp);
                    }

                    if (int.TryParse(loginData[9], out int maxExp))
                    {
                        GameData.MaxEXP = Mathf.Max(1, maxExp);
                    }
                }

                ApplyAttributeData(loginData, 10);
                ApplyCurrencyData(loginData, 15);
                GameData.NotifyChanged();

                PlayerHUD hud = FindAnyObjectByType<PlayerHUD>();
                if (hud != null)
                {
                    hud.UpdateLocalHUD();
                    hud.ShowHUD();
                }
            }
        }
        else if (parts[0] == "PLAYER_HP" && parts.Length > 1)
        {
            string[] hpData = parts[1].Split(',');
            if (hpData.Length >= 2 && int.TryParse(hpData[0], out int playerId) && int.TryParse(hpData[1], out int hpValue))
            {
                if (playerId == myPlayerId)
                {
                    GameData.CurrentHP = Mathf.Clamp(hpValue, 0, GameData.MaxHP);
                    PlayerHUD hud = FindAnyObjectByType<PlayerHUD>();
                    if (hud != null)
                    {
                        hud.UpdateLocalHUD();
                    }
                }
            }
        }
        else if (parts[0] == "PLAYER_PROGRESS" && parts.Length > 1)
        {
            string[] progressData = parts[1].Split(',');
            if (progressData.Length >= 7 && int.TryParse(progressData[0], out int playerId) && playerId == myPlayerId)
            {
                if (int.TryParse(progressData[1], out int level)) GameData.Level = Mathf.Max(1, level);
                if (int.TryParse(progressData[2], out int currentExp)) GameData.CurrentEXP = Mathf.Max(0, currentExp);
                if (int.TryParse(progressData[3], out int maxExp)) GameData.MaxEXP = Mathf.Max(1, maxExp);
                if (int.TryParse(progressData[4], out int currentHp)) GameData.CurrentHP = Mathf.Max(0, currentHp);
                if (int.TryParse(progressData[5], out int maxHp)) GameData.MaxHP = Mathf.Max(1, maxHp);
                ApplyAttributeData(progressData, 7);
                ApplyCurrencyData(progressData, 12);
                GameData.NotifyChanged();

                PlayerHUD hud = FindAnyObjectByType<PlayerHUD>();
                if (hud != null) hud.UpdateLocalHUD();
                Debug.Log($"[Progresion] +{progressData[6]} EXP. Nivel actual: {GameData.Level}");
            }
        }
        else if (parts[0] == "INVENTORY_STATE" && parts.Length > 1)
        {
            InventoryData.ApplySerialized(parts[1]);
            Debug.Log($"[Inventario] Estado recibido: {InventoryData.SlotCount} slots.");
        }
        else if (parts[0] == "DROP_REWARD" && parts.Length > 1)
        {
            string[] dropData = parts[1].Split(',');
            if (dropData.Length >= 2 && int.TryParse(dropData[1], out int quantity))
            {
                ItemDefinition definition = ItemCatalog.Get(dropData[0]);
                Debug.Log($"[Recompensa] Recibido: {definition.DisplayName} x{quantity} ({dropData[0]}).");
            }
        }
        else if (parts[0] == "DROP_FAILED" && parts.Length > 1)
        {
            string[] dropData = parts[1].Split(',');
            if (dropData.Length >= 2 && dropData[1] == "INVENTORY_FULL")
            {
                ItemDefinition definition = ItemCatalog.Get(dropData[0]);
                Debug.LogWarning($"[Recompensa] No hay espacio para {definition.DisplayName}; el drop no se ha guardado.");
            }
        }
        else if (parts[0] == "EQUIPMENT_STATE" && parts.Length > 1)
        {
            EquipmentData.ApplySerialized(parts[1]);
            Debug.Log("[Equipo] Estado recibido.");
        }
        else if (parts[0] == "EQUIP_FAILED" && parts.Length > 1)
        {
            Debug.LogWarning($"[Equipo] {parts[1]}");
        }
        else if (parts[0] == "ATTACK_FAILED" && parts.Length > 1)
        {
            string message = parts[1] switch
            {
                "NEED_WEAPON" => "¡Necesitas un arma equipada!",
                "ABILITY_RESTRICTED" => "Esta habilidad no está disponible para tu clase o nivel.",
                _ => "¡Necesitas equipar el arma compatible con esta habilidad!"
            };
            FindAnyObjectByType<PlayerHUD>()?.ShowCombatMessage(message);
        }
        else if (parts[0] == "ATTRIBUTE_FAILED" && parts.Length > 1)
        {
            Debug.LogWarning($"[Atributos] {parts[1]}");
        }
        else if (parts[0] == "ATTRIBUTE_RESET_FAILED" && parts.Length > 1)
        {
            Debug.LogWarning($"[Atributos] {parts[1]}");
        }
        else if (parts[0] == "PLAYER_DEFEATED" && parts.Length > 1)
        {
            if (int.TryParse(parts[1], out int defeatedPlayerId) && defeatedPlayerId == myPlayerId)
            {
                GameData.CurrentHP = 0;
                PlayerHUD hud = FindAnyObjectByType<PlayerHUD>();
                if (hud != null)
                {
                    hud.UpdateLocalHUD();
                }
            }
        }
        else if (parts[0] == "PLAYER_RESPAWN" && parts.Length > 1)
        {
            string[] respawnData = parts[1].Split(',');
            if (respawnData.Length >= 5 && int.TryParse(respawnData[0], out int respawnPlayerId))
            {
                if (respawnPlayerId == myPlayerId)
                {
                    if (float.TryParse(respawnData[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float respawnX) &&
                        float.TryParse(respawnData[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float respawnY) &&
                        float.TryParse(respawnData[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float respawnZ))
                    {
                        if (localPlayer != null)
                        {
                            localPlayer.position = new Vector3(respawnX, respawnY, respawnZ);
                        }
                    }

                    GameData.CurrentHP = GameData.MaxHP;
                    PlayerHUD hud = FindAnyObjectByType<PlayerHUD>();
                    if (hud != null)
                    {
                        hud.UpdateLocalHUD();
                    }
                }
            }
        }
        else if (parts[0] == "SPAWN_PLAYER" && parts.Length > 1)
        {
            string[] playerData = parts[1].Split(',');
            if (playerData.Length >= 6 && int.TryParse(playerData[0], out int spawnId))
            {
                if (spawnId != myPlayerId)
                {
                    string username = playerData[1];
                    playerNames[spawnId] = username;

                    float x = float.Parse(playerData[3], CultureInfo.InvariantCulture);
                    float y = float.Parse(playerData[4], CultureInfo.InvariantCulture) + remoteYOffset;
                    float z = float.Parse(playerData[5], CultureInfo.InvariantCulture);

                    GetOrSpawnRemotePlayer(spawnId, new Vector3(x, y, z), username);
                }
            }
        }
        else if (parts[0] == "MOVE_UPDATE" && parts.Length > 1)
        {
            string[] data = parts[1].Split(',');
            if (data.Length >= 9 && int.TryParse(data[0], out int id) && id != myPlayerId)
            {
                float x = float.Parse(data[1], CultureInfo.InvariantCulture);
                float y = float.Parse(data[2], CultureInfo.InvariantCulture) + remoteYOffset;
                float z = float.Parse(data[3], CultureInfo.InvariantCulture);
                float rotY = float.Parse(data[4], CultureInfo.InvariantCulture);
                
                bool.TryParse(data[5], out bool isMoving);
                bool.TryParse(data[6], out bool isRunning);
                bool.TryParse(data[7], out bool isFalling);
                bool.TryParse(data[8], out bool jumpTrigger);

                Vector3 targetPos = new Vector3(x, y, z);
                string username = playerNames.ContainsKey(id) ? playerNames[id] : "Jugador " + id;
                
                GameObject remoteObj = GetOrSpawnRemotePlayer(id, targetPos, username);
                NetworkPlayer netPlayer = remoteObj.GetComponent<NetworkPlayer>();
                if (netPlayer != null)
                {
                    netPlayer.UpdateNetworkState(targetPos, rotY, isMoving, isRunning, isFalling, jumpTrigger);
                }
            }
        }
        else if (parts[0] == "RECEIVE_ATTACK_BROADCAST" && parts.Length > 1)
        {
            string[] attackData = parts[1].Split(',');
            if (attackData.Length >= 4 &&
                int.TryParse(attackData[0], out int attackerId) &&
                int.TryParse(attackData[2], out int targetId) &&
                int.TryParse(attackData[3], out int slot) &&
                remotePlayers.TryGetValue(attackerId, out GameObject attackerObject))
            {
                NetworkPlayer attacker = attackerObject.GetComponent<NetworkPlayer>();
                if (attacker != null)
                {
                    string weaponItemId = attackData.Length > 4 ? attackData[4] : "";
                    attacker.PlayRemoteAttack(attackData[1], slot, weaponItemId, FindEntityTransform(targetId));
                }
            }
        }
        else if (parts[0] == "ENEMY_STATE" && parts.Length > 1)
        {
            string[] enemyStateData = parts[1].Split(',');
            if (enemyStateData.Length >= 6 &&
                int.TryParse(enemyStateData[0], out int enemyId) &&
                int.TryParse(enemyStateData[1], out int enemyState) &&
                int.TryParse(enemyStateData[2], out int targetId) &&
                float.TryParse(enemyStateData[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(enemyStateData[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(enemyStateData[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                NetworkEnemy enemy = FindNetworkEnemy(enemyId);
                if (enemy != null)
                {
                    enemy.ApplyServerState(enemyState, targetId, new Vector3(x, y + 0.05f, z));
                }
            }
        }
        else if (parts[0] == "ENEMY_ATTACK" && parts.Length > 1)
        {
            string[] enemyAttackData = parts[1].Split(',');
            if (enemyAttackData.Length >= 3 &&
                int.TryParse(enemyAttackData[0], out int enemyId) &&
                int.TryParse(enemyAttackData[1], out int playerTargetId))
            {
                NetworkEnemy enemy = FindNetworkEnemy(enemyId);
                if (enemy != null)
                {
                    enemy.ApplyServerState((int)EnemyAiState.Attack, playerTargetId, enemy.transform.position);
                    enemy.PlayAttackVisual();
                }

                if (playerTargetId == myPlayerId)
                {
                    Debug.Log($"[Red] El enemigo {enemyId} te ha golpeado.");
                }
            }
        }
        else if (parts.Length == 3 && parts[1] == "POS")
        {
            if (int.TryParse(parts[0], out int id) && id != myPlayerId)
            {
                string[] coords = parts[2].Split(',');
                float x = float.Parse(coords[0], CultureInfo.InvariantCulture);
                float y = float.Parse(coords[1], CultureInfo.InvariantCulture) + remoteYOffset;
                float z = float.Parse(coords[2], CultureInfo.InvariantCulture);

                Vector3 targetPos = new Vector3(x, y, z);
                string username = playerNames.ContainsKey(id) ? playerNames[id] : "Jugador " + id;
                
                GameObject remoteObj = GetOrSpawnRemotePlayer(id, targetPos, username);
                NetworkPlayer netPlayer = remoteObj.GetComponent<NetworkPlayer>();
                if (netPlayer != null)
                {
                    netPlayer.SetTargetPosition(targetPos);
                }
            }
        }
        else if (parts.Length == 2 && (parts[0] == "DISCONNECT" || parts[0] == "DESCONNECT"))
        {
            if (int.TryParse(parts[1], out int idToDestroy))
            {
                if (remotePlayers.ContainsKey(idToDestroy))
                {
                    Destroy(remotePlayers[idToDestroy]);
                    remotePlayers.Remove(idToDestroy);
                    playerNames.Remove(idToDestroy);
                    Debug.Log($"[Red] Jugador {idToDestroy} destruido.");
                }
            }
        }
        // ---------- AQUI EMPIEZA LO DEL ENEMIGO (Modificado) ----------
        else if (parts[0] == "SPAWN_ENEMY" && parts.Length > 1)
        {
            string[] enemyData = parts[1].Split(',');
            if (enemyData.Length >= 7 && int.TryParse(enemyData[0], out int enemyId) &&
                int.TryParse(enemyData[1], out int enemyTypeId) &&
                int.TryParse(enemyData[2], out int enemyMaxHp) && int.TryParse(enemyData[3], out int enemyLevel))
            {
                string enemyType = enemyData[1];
                float x = float.Parse(enemyData[4], CultureInfo.InvariantCulture);
                float y = float.Parse(enemyData[5], CultureInfo.InvariantCulture) + 0.05f;
                float z = float.Parse(enemyData[6], CultureInfo.InvariantCulture);

                Vector3 spawnPos = new Vector3(x, y, z);

                // 1. Validamos que el diccionario exista para que Unity no colapse
                if (activeEnemies == null) 
                {
                    activeEnemies = new Dictionary<int, GameObject>();
                }

                GameObject prefabToSpawn = enemyTypeId == 3 ? wolfPrefab : enemyPrefab;
                if (prefabToSpawn == null)
                {
                    Debug.LogError($"[Red] No hay prefab asignado para el tipo de enemigo {enemyType} en el Inspector de Unity.");
                    return;
                }

                if (!activeEnemies.ContainsKey(enemyId))
                {
                    GameObject newEnemy = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
                    
                    // 3. Validamos si creaste el script NetworkEnemy o no
                    NetworkEnemy netEnemy = newEnemy.GetComponent<NetworkEnemy>() ?? newEnemy.GetComponentInChildren<NetworkEnemy>();
                    if (netEnemy != null)
                    {
                        netEnemy.Initialize(enemyId, enemyType, enemyMaxHp, enemyLevel);
                    }
                    else
                    {
                        Debug.LogWarning("[Red] ADVERTENCIA: El Prefab instanciado no tiene el script 'NetworkEnemy' adjunto. Aparecerá en el mapa pero no hará nada.");
                    }

                    activeEnemies.Add(enemyId, newEnemy);
                    Debug.Log($"[Red] EXITAZO -> Enemigo spawneado: {enemyType} (ID: {enemyId}) en coordenadas {spawnPos}");
                }
            }
        }
        else if (parts[0] == "UPDATE_ENEMY_HP" && parts.Length > 1)
        {
            string[] hpData = parts[1].Split(',');
            if (hpData.Length == 2 && int.TryParse(hpData[0], out int enemyId) &&
                int.TryParse(hpData[1], out int currentHP) &&
                activeEnemies.TryGetValue(enemyId, out GameObject enemyObject))
            {
                NetworkEnemy enemy = enemyObject.GetComponent<NetworkEnemy>();
                if (enemy != null)
                {
                    int previousHP = enemy.currentHP;
                    enemy.currentHP = Mathf.Clamp(currentHP, 0, enemy.maxHP);
                    int damage = previousHP - enemy.currentHP;
                    if (damage > 0)
                    {
                        enemy.TakeDamageVisual();
                        SpawnDamageText(enemy.transform.position + Vector3.up * 1.5f, damage);
                    }

                    PlayerHUD hud = FindAnyObjectByType<PlayerHUD>();
                    if (hud != null && hud.CurrentTarget == enemy)
                    {
                        hud.UpdateTargetHP(enemy.currentHP);
                    }
                }
            }
        }
        else if (parts[0] == "ENEMY_DIED" && parts.Length > 1 &&
                 int.TryParse(parts[1], out int deadEnemyId) &&
                 activeEnemies.TryGetValue(deadEnemyId, out GameObject deadEnemyObject))
        {
            NetworkEnemy enemy = deadEnemyObject.GetComponent<NetworkEnemy>();
            PlayerHUD hud = FindAnyObjectByType<PlayerHUD>();
            if (hud != null && enemy != null)
            {
                hud.ClearTargetIf(enemy);
            }

            activeEnemies.Remove(deadEnemyId);
            if (enemy != null)
            {
                enemy.DieVisual(5f);
            }
            else
            {
                Destroy(deadEnemyObject, 5f);
            }
        }
    }

    private void SpawnDamageText(Vector3 position, int damage)
    {
        if (damageTextPrefab == null) return;

        GameObject damageObject = Instantiate(damageTextPrefab, position, Quaternion.identity);
        DamageText damageTextBehaviour = damageObject.GetComponent<DamageText>();
        if (damageTextBehaviour != null)
        {
            damageTextBehaviour.SetDamage(damage);
            return;
        }

        TMP_Text damageText = damageObject.GetComponentInChildren<TMP_Text>();
        if (damageText != null)
        {
            damageText.text = damage.ToString();
        }
    }

    private GameObject GetOrSpawnRemotePlayer(int id, Vector3 spawnPos, string username)
    {
        if (!remotePlayers.ContainsKey(id))
        {
            GameObject newPlayer = Instantiate(jugadorPrefab, spawnPos, Quaternion.identity);
            newPlayer.name = "JugadorRemoto_" + id;

            // Desactivar scripts locales en el clon
            LocalMovement localMove = newPlayer.GetComponent<LocalMovement>();
            if (localMove != null) Destroy(localMove);

            CharacterController controller = newPlayer.GetComponent<CharacterController>();
            if (controller != null) Destroy(controller);

            Rigidbody rb = newPlayer.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // Asegurar Collider para clics
            if (newPlayer.GetComponent<Collider>() == null)
            {
                CapsuleCollider col = newPlayer.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 1f, 0);
                col.height = 2f;
            }

            // Componente de sincronización en red
            NetworkPlayer netComp = newPlayer.GetComponent<NetworkPlayer>();
            if (netComp == null) netComp = newPlayer.AddComponent<NetworkPlayer>();
            netComp.enabled = true;
            PlayerCombat localCombat = localPlayer != null ? localPlayer.GetComponent<PlayerCombat>() : null;
            if (localCombat != null)
            {
                netComp.SetVisualReferences(
                    localCombat.basicArrowPrefab,
                    localCombat.skill1Prefab,
                    localCombat.skill2Prefab);
            }
            netComp.SetPlayerName(username);

            // Integración de tu Targetable.cs original
            Targetable targetable = newPlayer.GetComponent<Targetable>();
            if (targetable == null) targetable = newPlayer.AddComponent<Targetable>();
            
            targetable.isLocalPlayer = false;
            targetable.networkId = id;
            targetable.entityName = username;
            targetable.level = 1;
            targetable.currentHP = 100;
            targetable.maxHP = 100;
            if (targetable.nameplateText != null)
            {
                targetable.nameplateText.text = username;
            }

            remotePlayers[id] = newPlayer;
            Debug.Log($"[Red] Jugador remoto '{username}' (ID: {id}) instanciado.");
        }

        return remotePlayers[id];
    }

    public Transform GetEntityTransform(int entityId)
    {
        return FindEntityTransform(entityId);
    }

    private NetworkEnemy FindNetworkEnemy(int enemyId)
    {
        if (!activeEnemies.TryGetValue(enemyId, out GameObject enemyObject) || enemyObject == null)
        {
            return null;
        }

        return enemyObject.GetComponent<NetworkEnemy>() ?? enemyObject.GetComponentInChildren<NetworkEnemy>();
    }

    private Transform FindEntityTransform(int entityId)
    {
        if (entityId == myPlayerId && localPlayer != null)
        {
            return localPlayer;
        }

        if (remotePlayers.TryGetValue(entityId, out GameObject playerObject) && playerObject != null)
        {
            return playerObject.transform;
        }

        if (activeEnemies.TryGetValue(entityId, out GameObject enemyObject) && enemyObject != null)
        {
            return enemyObject.transform;
        }

        return null;
    }

    void OnDestroy() => Desconectar();
    void OnApplicationQuit() => Desconectar();

    void Desconectar()
    {
        if (client != null && client.Connected && stream != null)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes("QUIT\n");
                stream.Write(data, 0, data.Length);
            }
            catch (Exception) { }

            reader?.Close();
            stream?.Close();
            client?.Close();
        }
    }
}