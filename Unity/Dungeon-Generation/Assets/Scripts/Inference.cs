using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

using TMPro;

public class Inference : Agent
{
    // setting
    public float ChangePercentage = 50;
    public int RoomScale = 9;
    public DungeonSystem dungeonSystem;

    // 0: start room
    // 1: end room
    // 2: item room
    // 3: enemy room
    public int RoomType;

    public GameObject Target;

    public List<GameObject> Door = new List<GameObject>();
    public List<GameObject> Enemy = new List<GameObject>();
    public List<GameObject> Floor = new List<GameObject>();
    public GameObject Player;
    public GameObject Wall;
    public List<GameObject> WallOut = new List<GameObject>();
    public GameObject Item;
    public GameObject End;

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

    private bool cancle = false;


    public void Start()
    {
        Target = Instantiate(Target);
        dungeonSystem = GameObject.Find("DungeonSystem").GetComponent<DungeonSystem>();
        Time.timeScale = 10f;
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

        GenerateRandomRoom();

        float playerCount, endCount, enemyCount, itemCount;
        playerCount = endCount = enemyCount = itemCount = 0;
        for (int i = 0; i < RoomScale; i++)
        {
            for (int j = 0; j < RoomScale; j++)
            {
                int tile = currentRoomInformation[i][j];
                // 0: end
                if (tile == 0)
                    endCount += 1;
                // 1: enemy
                else if (tile == 1)
                    enemyCount += 1;
                // 3: player
                else if (tile == 3)
                    playerCount += 1;
                // 5: item
                else if (tile == 5)
                    itemCount += 1;
            }
        }

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

                if(playerCount <= 0)
                {
                    cancle = true;
                }
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

                if (endCount <= 0)
                {
                    cancle = true;
                }
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

                if (itemCount <= 0)
                {
                    cancle = true;
                }
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

                if (enemyCount <= 0)
                {
                    cancle = true;
                }
                break;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // room information to observation
        foreach (List<int> row in currentRoomInformation)
        {
            foreach (int tile in row)
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
        if (cancle)
        {
            cancle = false;
            EndEpisode();
        }
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
        CreateGameOjbect();

        // calc new reward
        float playerNewCount, regionNewCount, endNewCount, enemyNewCount, itemNewCount;
        playerNewCount = regionNewCount = endNewCount = enemyNewCount = itemNewCount = 0;
        for (int i = 0; i < RoomScale; i++)
        {
            for (int j = 0; j < RoomScale; j++)
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

        string answerTotal = (isPlayerGood && isRegionGood && isEndGood && isEnemyGood && isItemGood) ? "yes" : "no";

        for (int i = 0; i < 4; i++)
        {
            List<int> DoorPosX = new List<int>(new int[] { 0, RoomScale - 1, RoomScale / 2, RoomScale / 2 });
            List<int> DoorPosY = new List<int>(new int[] { RoomScale / 2, RoomScale / 2, 0, RoomScale - 1 });
            if (currentRoomInformation[DoorPosX[i]][DoorPosY[i]] != 2)
            {
                AddReward(-0.1f);
            }
        }
        if (answerTotal.Equals("yes"))
        {
            dungeonSystem.generateAble = true;
            Destroy(Target);
            gameObject.GetComponent<Inference>().enabled = false;
        }
        if (currentChangeCount <= 0)
        {
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
                        Instantiate(End, position, Quaternion.identity, transform);
                        Instantiate(Floor[Random.Range(0, 2)], position + new Vector3(0, 0, 1), Quaternion.identity, transform);
                        break;
                    case 1:
                        Instantiate(Enemy[Random.Range(0, 2)], position, Quaternion.identity, transform);
                        Instantiate(Floor[Random.Range(0, 2)], position + new Vector3(0, 0, 1), Quaternion.identity, transform);
                        break;
                    case 2:
                        Instantiate(Floor[Random.Range(0, 2)], position, Quaternion.identity, transform);
                        break;
                    case 3:
                        Instantiate(Player, position, Quaternion.identity, transform);
                        Instantiate(Floor[Random.Range(0, 2)], position + new Vector3(0, 0, 1), Quaternion.identity, transform);
                        break;
                    case 4:
                        Instantiate(Wall, position, Quaternion.identity, transform);
                        break;
                    case 5:
                        Instantiate(Item, position, Quaternion.identity, transform);
                        Instantiate(Floor[Random.Range(0, 2)], position + new Vector3(0, 0, 1), Quaternion.identity, transform);
                        break;
                    default:
                        break;
                }
            }
        }
        for(int i = -1; i < RoomScale + 1; i++)
        {
            for(int j = -1; j < RoomScale + 1; j++)
            {
                if (i == -1 || i == RoomScale || j == -1 || j == RoomScale)
                {
                    Vector3 position = new Vector3(i, j, 0) + transform.position;
                    if (i != RoomScale / 2 && j != RoomScale / 2)
                    {
                        if(i == -1 && j == -1)
                            Instantiate(WallOut[0], position, Quaternion.identity, transform);
                        else if (i == -1 && j == RoomScale)
                            Instantiate(WallOut[1], position, Quaternion.identity, transform);
                        else if (i == RoomScale && j == -1)
                            Instantiate(WallOut[2], position, Quaternion.identity, transform);
                        else if (i == RoomScale && j == RoomScale)
                            Instantiate(WallOut[3], position, Quaternion.identity, transform);
                        else if (i == -1)
                            Instantiate(WallOut[4], position, Quaternion.identity, transform);
                        else if (i == RoomScale)
                            Instantiate(WallOut[5], position, Quaternion.identity, transform);
                        else if (j == -1)
                            Instantiate(WallOut[6], position, Quaternion.identity, transform);
                        else if (j == RoomScale)
                            Instantiate(WallOut[7], position, Quaternion.identity, transform);
                    }
                    else
                    {
                        if(i == RoomScale / 2 && j == -1)
                            Instantiate(Door[0], position, Quaternion.identity, transform);
                        else if (i == RoomScale / 2 && j == RoomScale)
                            Instantiate(Door[0], position, Quaternion.identity, transform);
                        else if (i == -1 && j == RoomScale / 2)
                            Instantiate(Door[1], position, Quaternion.identity, transform);
                        else if (i == RoomScale && j == RoomScale / 2)
                            Instantiate(Door[2], position, Quaternion.identity, transform);
                    }
                }
            }
        }
    }
}
