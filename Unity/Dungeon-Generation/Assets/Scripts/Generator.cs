using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

using TMPro;

public class Generator : Agent
{
    // calc
    public float sumPercentage = 0;
    public float meanPercentage = 0;
    public float stdPercentage = 0;
    private List<float> listPercentage = new List<float>();
    private int dataCount = 0;
    private int interval = 100;

    // debug
    public TextMeshProUGUI rewardText;
    public bool isInference = false;

    // setting
    public float ChangePercentage = 50;
    public int RoomScale = 9;

    // 0: start room
    // 1: end room
    // 2: item room
    // 3: enemy room
    public int RoomType;

    public GameObject Target;

    public GameObject Door;
    public GameObject Enemy;
    public GameObject Floor;
    public GameObject Player;
    public GameObject Wall;
    public GameObject Item;

    // Reward Range
    private float minPlayer;
    private float maxPlayer;
    private float minRegion;
    private float maxRegion;
    private float minEnd;
    private float maxEnd;
    private float minEnemy;
    private float maxEnemy;
    private float minItem;
    private float maxItem;

    // 0: door
    // 1: enemy
    // 2: floor
    // 3: player
    // 4: wall
    // 5: item
    private List<float> probList = new List<float>(new float[] { 0.01f, 0.03f, 0.66f, 0.01f, 0.3f, 0.01f });
    private int currentChangeCount;
    private List<List<int>> currentRoomInformation = new List<List<int>>();
    private List<List<int>> currentRoomHeatmap = new List<List<int>>();


    public void Start()
    {
        Target = Instantiate(Target);
        if (isInference)
        {
            Time.timeScale = 20f;
            interval = 1000;
        }
    }

    public int SelectRandomElement()
    {
        float totalProbability = 0f;

        // 확률의 합을 계산
        foreach (float probability in probList)
        {
            totalProbability += probability;
        }

        // 0과 1 사이의 랜덤한 값을 생성
        float randomValue = Random.value;

        float cumulativeProbability = 0f;

        // 각 요소의 확률을 누적하여 랜덤한 값을 구간에 맞게 선택
        for (int i = 0; i < probList.Count; i++)
        {
            cumulativeProbability += probList[i] / totalProbability;
            if (randomValue <= cumulativeProbability)
            {
                return i;
            }
        }

        // 선택되지 않았을 경우 마지막 요소를 반환
        return probList.Count - 1;
    }
    public void GenerateRandomRoom()
    {
        for (int i = 0; i < RoomScale; i++)
        {
            List<int> row = new List<int>();
            List<int> zeroRow = new List<int>();
            for (int j = 0; j < RoomScale; j++)
            {
                row.Add(SelectRandomElement());
                zeroRow.Add(0);
            }
            currentRoomInformation.Add(row);
            currentRoomHeatmap.Add(zeroRow);
        }
    }
    public override void OnEpisodeBegin()
    {
        currentRoomInformation = new List<List<int>>();
        currentRoomHeatmap = new List<List<int>>();
        currentChangeCount = Mathf.RoundToInt(RoomScale * RoomScale * ChangePercentage / 100 * 5);

        RoomType = Random.Range(0, 4);
        switch (RoomType)
        {
            // 0: start room
            case 0:
                minPlayer = 1;
                maxPlayer = 1;
                minRegion = 1;
                maxRegion = 1;
                minEnd = 0;
                maxEnd = 0;
                minEnemy = 0;
                maxEnemy = 0;
                minItem = 0;
                maxItem = 0;
                break;

            // 1: end room
            case 1:
                minPlayer = 0;
                maxPlayer = 0;
                minRegion = 1;
                maxRegion = 1;
                minEnd = 1;
                maxEnd = 1;
                minEnemy = 0;
                maxEnemy = 0;
                minItem = 0;
                maxItem = 0;
                break;

            // 2: item room
            case 2:
                minPlayer = 0;
                maxPlayer = 0;
                minRegion = 1;
                maxRegion = 1;
                minEnd = 0;
                maxEnd = 0;
                minEnemy = 0;
                maxEnemy = 0;
                minItem = 1;
                maxItem = 1;
                break;

            // 3: enemy room
            case 3:
                minPlayer = 0;
                maxPlayer = 0;
                minRegion = 1;
                maxRegion = 1;
                minEnd = 0;
                maxEnd = 0;
                minEnemy = 2;
                maxEnemy = 3;
                minItem = 0;
                maxItem = 0;
                break;
        }
        GenerateRandomRoom();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // room information to observation
        foreach(List<int> row in currentRoomInformation)
        {
            foreach(int tile in row)
            {
                sensor.AddObservation(tile);
            }
        }

        // room heatmap to observation
        foreach (List<int> row in currentRoomHeatmap)
        {
            foreach (int tile in row)
            {
                sensor.AddObservation(tile);
            }
        }
        for (int i = 0; i < 4; i++)
        {
            if (i == RoomType)
                sensor.AddObservation(1);
            else
                sensor.AddObservation(0);
        }
    }

    int CountRegions(int targetItem)
    {
        int regionCount = 0;
        int numRows = currentRoomInformation.Count;
        int numCols = currentRoomInformation[0].Count;

        bool[,] visited = new bool[numRows, numCols];

        // 2차원 리스트를 순회하면서 연결된 영역 개수를 세기 위해 DFS(Depth-First Search) 알고리즘을 사용
        for (int i = 0; i < numRows; i++)
        {
            for (int j = 0; j < numCols; j++)
            {
                if (currentRoomInformation[i][j] != targetItem && !visited[i, j])
                {
                    regionCount++;
                    DFS(i, j, targetItem, visited);
                }
            }
        }

        return regionCount;
    }

    void DFS(int row, int col, int targetItem, bool[,] visited)
    {
        int numRows = currentRoomInformation.Count;
        int numCols = currentRoomInformation[0].Count;

        visited[row, col] = true;

        // 현재 위치의 상하좌우를 확인하면서 연결된 영역을 탐색
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        for (int k = 0; k < 4; k++)
        {
            int newRow = row + dx[k];
            int newCol = col + dy[k];

            // 유효한 범위 내에 있고, 탐색하지 않은 영역이며, 타겟 아이템과 같은 경우 재귀적으로 DFS 호출
            if (newRow >= 0 && newRow < numRows && newCol >= 0 && newCol < numCols &&
                !visited[newRow, newCol] && currentRoomInformation[newRow][newCol] != targetItem)
            {
                DFS(newRow, newCol, targetItem, visited);
            }
        }
    }

    public bool isCorrectRoom(float value, float min, float max)
    {
        if (min <= value && value <= max)
            return true;
        else
            return false;
    }
    public float RewardRange(float oldValue, float newValue, float min, float max)
    {
        if (newValue >= min && newValue <= max && oldValue >= min && oldValue <= max)
            return 0;
        if (oldValue <= max && newValue <= max)
            return Mathf.Min(newValue, min) - Mathf.Min(oldValue, min);
        if (oldValue >= min && newValue >= min)
            return Mathf.Max(oldValue, max) - Mathf.Max(newValue, max);
        if (newValue > max && oldValue < min)
            return max - newValue + oldValue - min;
        if (newValue < min && oldValue > max)
            return max - oldValue + newValue - min;

        return 0;
    }

    public void DoAction(int moveXAction, int moveYAction, int changeAction)
    {
        Target.transform.position = new Vector3(moveXAction, moveYAction, -1) + transform.position;
        currentRoomInformation[moveXAction][moveYAction] = changeAction;
        currentRoomHeatmap[moveXAction][moveYAction] += 1;
        currentChangeCount -= 1;
    }
    public override void OnActionReceived(ActionBuffers actions)
    {

        // calc old reward
        float playerOldCount, regionOldCount, endOldCount, enemyOldCount, itemOldCount;
        playerOldCount = regionOldCount = endOldCount = enemyOldCount = itemOldCount = 0;
        for (int i = 0; i < RoomScale; i++)
        {
            for (int j = 0; j < RoomScale; j++)
            {
                int tile = currentRoomInformation[i][j];
                // 0: end
                if (tile == 0)
                    endOldCount += 1;
                // 1: enemy
                else if (tile == 1)
                    enemyOldCount += 1;
                // 3: player
                else if (tile == 3)
                    playerOldCount += 1;
                // 5: item
                else if (tile == 5)
                    itemOldCount += 1;
            }
        }
        regionOldCount = CountRegions(4);

        // Do action
        int moveXAction = actions.DiscreteActions[0];
        int moveYAction = actions.DiscreteActions[1];
        int changeAction = actions.DiscreteActions[2];
        DoAction(moveXAction, moveYAction, changeAction);

        // calc new reward
        float playerNewCount, regionNewCount, endNewCount, enemyNewCount, itemNewCount;
        playerNewCount = regionNewCount = endNewCount = enemyNewCount = itemNewCount = 0;
        for(int i = 0; i < RoomScale; i++)
        {
            for(int j = 0; j < RoomScale; j++)
            {
                int tile = currentRoomInformation[i][j];
                // 0: end
                if (tile == 0)
                    endNewCount += 1;
                // 1: enemy
                else if (tile == 1)
                    enemyNewCount += 1;
                // 3: player
                else if (tile == 3)
                    playerNewCount += 1;
                // 5: item
                else if (tile == 5)
                    itemNewCount += 1;
            }
        }
        regionNewCount = CountRegions(4);

        // calc reward
        float playerReward = RewardRange(playerOldCount, playerNewCount, minPlayer, maxPlayer) * 3;
        float regionReward = RewardRange(regionOldCount, regionNewCount, minRegion, maxRegion) * 5;
        float endReward = RewardRange(endOldCount, endNewCount, minEnd, maxEnd) * 3;
        float enemyReward = RewardRange(enemyOldCount, enemyNewCount, minEnemy, maxEnemy) * 3;
        float itemReward = RewardRange(itemOldCount, itemNewCount, minItem, maxItem) * 3;
        float totalReward = playerReward + regionReward + endReward + enemyReward + itemReward;
        AddReward(totalReward);

        bool isPlayerGood = isCorrectRoom(playerNewCount, minPlayer, maxPlayer);
        bool isRegionGood = isCorrectRoom(regionNewCount, minRegion, maxRegion);
        bool isEndGood = isCorrectRoom(endNewCount, minEnd, maxEnd);
        bool isEnemyGood = isCorrectRoom(enemyNewCount, minEnemy, maxEnemy);
        bool isItemGood = isCorrectRoom(itemNewCount, minItem, maxItem);

        string answerPlayer = (isPlayerGood) ? "yes" : "no";
        string answerRegion = (isRegionGood) ? "yes" : "no";
        string answerEnd = (isEndGood) ? "yes" : "no";
        string answerEnemy = (isEnemyGood) ? "yes" : "no";
        string answerItem = (isItemGood) ? "yes" : "no";
        string answerTotal = (isPlayerGood && isRegionGood && isEndGood && isEnemyGood && isItemGood) ? "yes" : "no";

        float answerPercentage = 0;
        answerPercentage += (isPlayerGood) ? 20 : 0;
        answerPercentage += (isRegionGood) ? 20 : 0;
        answerPercentage += (isEndGood) ? 20 : 0;
        answerPercentage += (isEnemyGood) ? 20 : 0;
        answerPercentage += (isItemGood) ? 20 : 0;

        for (int i = 0; i < 4; i++)
        {
            List<int> DoorPosX = new List<int>(new int[] { 0, RoomScale - 1, RoomScale / 2, RoomScale / 2 });
            List<int> DoorPosY = new List<int>(new int[] { RoomScale / 2, RoomScale / 2, 0, RoomScale - 1 });
            if(currentRoomInformation[DoorPosX[i]][DoorPosY[i]] != 2)
            {
                AddReward(-0.1f);
            }
        }

        if (isInference)
        {
            CreateGameOjbect();
            if (rewardText != null)
            {
                rewardText.text = "Room type: " + RoomType.ToString() + "\n" + "Player count: " + playerNewCount.ToString() + "\n" +
                    "Region count: " + regionNewCount.ToString() + "\n" + "End count: " + endNewCount.ToString() + "\n" +
                    "Enemy count: " + enemyNewCount.ToString() + "\n" + "Item count: " + itemNewCount.ToString() + "\n" +
                    "Is good room? " + answerTotal + "\n" + "Is good player?" + answerPlayer + "\n" +
                    "Is good region? " + answerRegion + "\n" + "Is good end?" + answerEnd + "\n" +
                    "Is good enemy? " + answerEnemy + "\n" + "Is good item?" + answerItem + "\n" +
                    "Answer percentage: " + answerPercentage.ToString() + "%" + "\n" +
                    "Mean answer percentage: " + meanPercentage.ToString() + "%";
            }
        }
        if (currentChangeCount <= 0 || answerTotal.Equals("yes"))
        {
/*            float percentage = (isPlayerGood && isRegionGood && isEndGood && isEnemyGood && isItemGood) ? 100 : 0;*/
            sumPercentage += answerPercentage;
            listPercentage.Add(answerPercentage);
            dataCount += 1;

            if(dataCount % interval == 0)
            {
                meanPercentage = sumPercentage / dataCount;
                for(int i = 0; i < listPercentage.Count; i++)
                {
                    stdPercentage += Mathf.Pow(listPercentage[i] - meanPercentage, 2);
                }
                stdPercentage = Mathf.Sqrt(stdPercentage / listPercentage.Count);
                print(this.transform.name + ", mean: " + meanPercentage.ToString() + ", std: " + stdPercentage.ToString());
                sumPercentage = 0;
                dataCount = 0;
            }

            if (!isInference)
            {
                CreateGameOjbect();
                if (rewardText != null)
                {
                    rewardText.text = "Room type: " + RoomType.ToString() + "\n" + "Player count: " + playerNewCount.ToString() + "\n" +
                        "Region count: " + regionNewCount.ToString() + "\n" + "End count: " + endNewCount.ToString() + "\n" +
                        "Enemy count: " + enemyNewCount.ToString() + "\n" + "Item count: " + itemNewCount.ToString() + "\n" +
                        "Is good room? " + answerTotal + "\n" + "Is good player?" + answerPlayer + "\n" +
                        "Is good region? " + answerRegion + "\n" + "Is good end?" + answerEnd + "\n" +
                        "Is good enemy? " + answerEnemy + "\n" + "Is good item?" + answerItem + "\n" +
                        "Answer percentage: " + answerPercentage.ToString() + "%" + "\n" +
                        "Mean answer percentage: " + meanPercentage.ToString() + "%";
                }
            }
            if (answerTotal.Equals("yes"))
            {
                AddReward(10);
            }
            EndEpisode();
        }
    }

    public void CreateGameOjbect()
    {
        // 0: end
        // 1: enemy
        // 2: floor
        // 3: player
        // 4: wall
        // 5: item
        // 기존에 생성된 게임 오브젝트 삭제
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        int numRows = currentRoomInformation.Count;
        int numCols = currentRoomInformation[0].Count;

        for (int i = 0; i < numRows; i++)
        {
            for (int j = 0; j < numCols; j++)
            {
                int item = currentRoomInformation[i][j];
                Vector3 position = new Vector3(i, j, 0) + transform.position; // 좌표를 Vector3로 변환

                // 아이템에 따라 해당 좌표에 새로운 게임 오브젝트 생성
                switch (item)
                {
                    case 0:
                        Instantiate(Door, position, Quaternion.identity, transform);
                        break;
                    case 1:
                        Instantiate(Enemy, position, Quaternion.identity, transform);
                        break;
                    case 2:
                        Instantiate(Floor, position, Quaternion.identity, transform);
                        break;
                    case 3:
                        Instantiate(Player, position, Quaternion.identity, transform);
                        break;
                    case 4:
                        Instantiate(Wall, position, Quaternion.identity, transform);
                        break;
                    case 5:
                        Instantiate(Item, position, Quaternion.identity, transform);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
