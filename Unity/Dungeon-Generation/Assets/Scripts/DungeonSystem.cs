using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonSystem : MonoBehaviour
{
    public GameObject Generator;
    public GameObject mainCamera;
    public bool generateAble;

    private int roomType;
    private int roomScale = 11;
    private List<int> roomList = new List<int>(new int[] {0, 3, 2, 3, 3, 2, 3, 2, 3, 3, 2, 1 });
    private List<List<int>> dungeon = new List<List<int>>();
    private List<int> dungeonX = new List<int>();
    private List<int> dungeonY = new List<int>();

    private Vector3 cameraTarget;
    // Start is called before the first frame update
    void Start()
    {
        for(int i = 0; i < roomList.Count * 3; i++)
        {
            List<int> raw = new List<int>();
            for(int j = 0; j < roomList.Count * 3; j++)
            {
                raw.Add(0);
            }
            dungeon.Add(raw);
        }

        int startX = Random.Range(0, roomList.Count * 2);
        int startY = Random.Range(0, roomList.Count * 2);
        dungeon[startX][startY] = 1;
        dungeonX.Add(startX);
        dungeonY.Add(startY);

        int count = 1;
        while (count < roomList.Count)
        {
            List<int> moveX = new List<int>(new int[] { 0, 0, 1, -1 });
            List<int> moveY = new List<int>(new int[] { 1, -1, 0, 0 });
            int moveIdx = Random.Range(0, 4);
            int roomIdx = Random.Range(0, dungeonX.Count);
            int nextX = Mathf.Clamp(dungeonX[roomIdx] + moveX[moveIdx], 0, roomList.Count * 2 - 1);
            int nextY = Mathf.Clamp(dungeonY[roomIdx] + moveY[moveIdx], 0, roomList.Count * 2 - 1);

            int temp = 0;
            while (dungeon[nextX][nextY] == 1)
            {
                if(temp > 10000)
                {
                    break;
                }
                moveIdx = Random.Range(0, 4);
                roomIdx = Random.Range(0, dungeonX.Count);
                nextX = Mathf.Clamp(dungeonX[roomIdx] + moveX[moveIdx], 0, roomList.Count * 2);
                nextY = Mathf.Clamp(dungeonY[roomIdx] + moveY[moveIdx], 0, roomList.Count * 2);
                temp += 1;
            }

            print(nextX.ToString() + ", " + nextY.ToString());
            dungeon[nextX][nextY] = 1;
            dungeonX.Add(nextX);
            dungeonY.Add(nextY);

            count += 1;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (generateAble && roomList.Count > 0)
        {
            generateAble = false;

            roomType = roomList[0];
            roomList.RemoveAt(0);

            GameObject model = Instantiate(Generator, new Vector2(dungeonX[0] * roomScale, dungeonY[0] * roomScale), Quaternion.identity);
            cameraTarget = new Vector3(dungeonX[0] * roomScale + roomScale / 2, dungeonY[0] * roomScale + roomScale / 2, -10f);
            model.GetComponent<Inference>().RoomType = roomType;

            dungeonX.RemoveAt(0);
            dungeonY.RemoveAt(0);
        }
        if (roomType == 0)
            mainCamera.transform.position = cameraTarget;
        if (roomList.Count > 0)
            mainCamera.transform.position = Vector3.MoveTowards(mainCamera.transform.position, cameraTarget, Time.deltaTime * 10);
        else
        {
            float moveX = Input.GetAxis("Horizontal"); // 수평 방향 입력 값 (-1 ~ 1)
            float moveY = Input.GetAxis("Vertical"); // 수직 방향 입력 값 (-1 ~ 1)
            float speed = 5f;
            
            Vector3 movement = new Vector3(moveX, moveY, 0f); // x와 z 축으로 움직임 벡터 생성
            movement = movement.normalized * speed * Time.deltaTime; // 속도와 프레임 간격에 따라 움직임 벡터 조정

            mainCamera.transform.Translate(movement); // 오브젝트 이동
        }
    }
}
