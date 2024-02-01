using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Graphs;
using Random = System.Random;
using System.Linq;
using System;


public class Generator3D : MonoBehaviour
{
    enum CellType
    {
        None,
        Room,
        Hallway,
        Stairs,
        Door
    }

    class Room
    {
        public BoundsInt bounds;

        public Room(Vector3Int location, Vector3Int size)
        {
            bounds = new BoundsInt(location, size);
        }

        public static bool Intersect(Room a, Room b)
        {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y)
                || (a.bounds.position.z >= (b.bounds.position.z + b.bounds.size.z)) || ((a.bounds.position.z + a.bounds.size.z) <= b.bounds.position.z));
        }
    }

    [SerializeField]
    private int seed;

    [SerializeField]
    private float scale = 4;
    [SerializeField]
    GameObject Player;

    [SerializeField]
    Vector3Int size;
    [SerializeField]
    int roomCount;
    [SerializeField]
    Vector3Int roomMaxSize;
    [SerializeField]
    GameObject floorPrefab;

    [SerializeField]
    GameObject hallwayPrefab;

    Grid3D<CellType> grid;
    List<Room> rooms;
    Delaunay3D delaunay;
    HashSet<Prim.Edge> selectedEdges;


    List<Vector3Int> hallwayPieces = new();
    List<Vector3Int> stairwayPieces = new();

    [SerializeField]
    GameObject wallPrefab;
    [SerializeField]
    GameObject doorPrefab;
    [SerializeField]
    GameObject hallwayWallPrefab;
    [SerializeField]
    GameObject stairwayPrefab;



    [SerializeField]
    GameObject startingRoom;

    [SerializeField]
    double loopRate;


    void Start()
    {
        if (seed == 0)
        {
            Random newSeed = new Random();
            seed = newSeed.Next(Int32.MinValue, Int32.MaxValue);
        }
        if (loopRate <= 0)
        {
            loopRate = 0.125;
        }
        UnityEngine.Random.InitState(seed);
        grid = new Grid3D<CellType>(size, Vector3Int.zero);
        rooms = new List<Room>();
        Generate();

        Debug.Log($"Rooms: {rooms.Count} \nHallway Pieces: {hallwayPieces.Count} \nStairway Pieces: {stairwayPieces.Count}");
    }
    void Generate()
    {
        Generator();
        Instantiator();

        var startPos = startingRoom.transform.position;
        startPos.x += scale / 2;
        startPos.z += scale / 2;
        startPos.y += scale / 2;
        Player.transform.position = startPos;
    }
    void Generator()
    {
        GenerateRooms();
        Triangulate();
        GenerateHallways();
        PathfindHallways();

    }
    void Instantiator()
    {
        InstantiateRooms();
        InstantiateHallways();
        InstantiateStairs();

    }
    void GenerateRooms()
    {
        for (int i = 0; i < roomCount; i++)
        {
            Vector3Int location = new Vector3Int(
                UnityEngine.Random.Range(0, size.x),
                UnityEngine.Random.Range(0, size.y),
               UnityEngine.Random.Range(0, size.z)
            );

            Vector3Int roomSize = new Vector3Int(
               UnityEngine.Random.Range(1, roomMaxSize.x + 1),
                UnityEngine.Random.Range(1, roomMaxSize.y + 1),
                UnityEngine.Random.Range(1, roomMaxSize.z + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector3Int(-1, 0, -1), roomSize + new Vector3Int(2, 0, 2));

            foreach (var room in rooms)
            {
                if (Room.Intersect(room, buffer))
                {
                    add = false;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y
                || newRoom.bounds.zMin < 0 || newRoom.bounds.zMax >= size.z)
            {
                add = false;
            }

            if (add)
            {
                rooms.Add(newRoom);
                //PlaceRoomWithWallsAndFloor(newRoom.bounds.position, newRoom.bounds.size);

                foreach (var pos in newRoom.bounds.allPositionsWithin)
                {
                    grid[pos] = CellType.Room;
                }
            }
        }
    }

    void Triangulate()
    {
        List<Vertex> vertices = new List<Vertex>();

        foreach (var room in rooms)
        {
            vertices.Add(new Vertex<Room>((Vector3)room.bounds.position + ((Vector3)room.bounds.size) / 2, room));
        }

        delaunay = Delaunay3D.Triangulate(vertices);
    }

    void GenerateHallways()
    {
        List<Prim.Edge> edges = new List<Prim.Edge>();

        foreach (var edge in delaunay.Edges)
        {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        List<Prim.Edge> minimumSpanningTree = Prim.MinimumSpanningTree(edges, edges[0].U);

        selectedEdges = new HashSet<Prim.Edge>(minimumSpanningTree);
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges)
        {
            if (UnityEngine.Random.value < loopRate)
            {
                selectedEdges.Add(edge);
            }
        }
    }

    void PathfindHallways()
    {
        DungeonPathfinder3D aStar = new DungeonPathfinder3D(size);

        foreach (var edge in selectedEdges)
        {
            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;
            var startPos = new Vector3Int((int)startPosf.x, (int)startPosf.y, (int)startPosf.z);
            var endPos = new Vector3Int((int)endPosf.x, (int)endPosf.y, (int)endPosf.z);

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder3D.Node a, DungeonPathfinder3D.Node b) =>
            {
                var pathCost = new DungeonPathfinder3D.PathCost();

                var delta = b.Position - a.Position;

                if (delta.y == 0)
                {
                    //flat hallway
                    pathCost.cost = Vector3Int.Distance(b.Position, endPos);    //heuristic

                    if (grid[b.Position] == CellType.Stairs)
                    {
                        return pathCost;
                    }
                    else if (grid[b.Position] == CellType.Room)
                    {
                        pathCost.cost += 5;
                    }
                    else if (grid[b.Position] == CellType.None)
                    {
                        pathCost.cost += 1;
                    }

                    pathCost.traversable = true;
                }
                else
                {
                    //staircase
                    if ((grid[a.Position] != CellType.None && grid[a.Position] != CellType.Hallway)
                        || (grid[b.Position] != CellType.None && grid[b.Position] != CellType.Hallway)) return pathCost;

                    pathCost.cost = 100 + Vector3Int.Distance(b.Position, endPos);    //base cost + heuristic

                    int xDir = Mathf.Clamp(delta.x, -1, 1);
                    int zDir = Mathf.Clamp(delta.z, -1, 1);
                    Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);
                    Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);

                    if (!grid.InBounds(a.Position + verticalOffset)
                        || !grid.InBounds(a.Position + horizontalOffset)
                        || !grid.InBounds(a.Position + verticalOffset + horizontalOffset))
                    {
                        return pathCost;
                    }

                    if (grid[a.Position + horizontalOffset] != CellType.None
                        || grid[a.Position + horizontalOffset * 2] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset] != CellType.None
                        || grid[a.Position + verticalOffset + horizontalOffset * 2] != CellType.None)
                    {
                        return pathCost;
                    }

                    pathCost.traversable = true;
                    pathCost.isStairs = true;
                }

                return pathCost;
            });

            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    var current = path[i];

                    if (grid[current] == CellType.None)
                    {
                        grid[current] = CellType.Hallway;
                    }

                    if (i > 0)
                    {
                        var prev = path[i - 1];

                        var delta = current - prev;

                        if (delta.y != 0)
                        {
                            int xDir = Mathf.Clamp(delta.x, -1, 1);
                            int zDir = Mathf.Clamp(delta.z, -1, 1);
                            Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);
                            Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);

                            grid[prev + horizontalOffset] = CellType.Stairs;
                            grid[prev + horizontalOffset * 2] = CellType.Stairs;
                            grid[prev + verticalOffset + horizontalOffset] = CellType.Stairs;
                            grid[prev + verticalOffset + horizontalOffset * 2] = CellType.Stairs;

                            /*
                            PlaceStairs(prev + horizontalOffset);
                            PlaceStairs(prev + horizontalOffset * 2);
                            PlaceStairs(prev + verticalOffset + horizontalOffset);
                            PlaceStairs(prev + verticalOffset + horizontalOffset * 2);
                            */
                            //testing???
                            stairwayPieces.Add(prev + horizontalOffset);
                            stairwayPieces.Add(prev + horizontalOffset * 2);
                            stairwayPieces.Add(prev + verticalOffset + horizontalOffset);
                            stairwayPieces.Add(prev + verticalOffset + horizontalOffset * 2);
                        }

                        Debug.DrawLine((prev + new Vector3(0.5f, 0.5f, 0.5f))*scale, (current + new Vector3(0.5f, 0.5f, 0.5f))*scale, Color.blue, 100, false);
                    }
                }

                foreach (var pos in path)
                {
                    if (grid[pos] == CellType.Hallway)
                    {


                        hallwayPieces.Add(pos);
                    }
                }
            }
        }
    }

    void InstantiateHallways() //DFS traversal
    {
        HashSet<Vector3Int> visited = new();
        Stack<Vector3Int> traverseHallway = new();
        foreach (var check in hallwayPieces)
        {
            if (!visited.Contains(check))
            {
                traverseHallway.Push(check);
                GameObject Parent = new GameObject("Hallway");

                while (traverseHallway.Count > 0)
                {
                    var hallwayPiece = traverseHallway.Pop();

                    visited.Add(hallwayPiece);
                    GameObject go = InstantiateHallwayPiece(hallwayPiece);
                    go.transform.parent = Parent.transform;
                    foreach (var neighbor in GetNeighbors(hallwayPiece))
                    {

                        if (neighbor.x < 0 || neighbor.x >= size.x || neighbor.y < 0 || neighbor.y >= size.y || neighbor.z < 0 || neighbor.z >= size.z || grid[neighbor] == CellType.None)
                        {
                            //InstantiateHallwayWall(hallwayPiece, neighbor);
                            continue;
                        }
                        if (visited.Contains(neighbor) || grid[neighbor] != CellType.Hallway)
                        {
                            continue;
                        }
                        traverseHallway.Push(neighbor);

                    }
                }
            }
        }
    }

    List<Vector3Int> GetNeighbors(Vector3Int piece)
    {
        return new List<Vector3Int>{
        new Vector3Int(piece.x+1, piece.y,piece.z),
        new Vector3Int(piece.x-1, piece.y,piece.z),
        new Vector3Int(piece.x, piece.y+1,piece.z),
        new Vector3Int(piece.x, piece.y-1,piece.z)
       // new Vector3Int(piece.x, piece.y,piece.z+1),
      //  new Vector3Int(piece.x, piece.y,piece.z-1)
        };

    }

    void InstantiateRooms()
    {
        foreach (var room in rooms)
        {
            var roomObj = InstantiateRoom(room.bounds.position, room.bounds.size);
            if (startingRoom == null)
            {
                startingRoom = roomObj;
            }
        }
    }
    GameObject InstantiateRoom(Vector3Int location, Vector3Int size)
    {
        var spot = location;
        GameObject parentObject = new GameObject("Room");
        parentObject.transform.position = new Vector3(spot.x, spot.y, spot.z) * scale;
        for (int z = 0; z < size.y; z++)
        {
            for (int x = 0; x < size.x; x++)
            {

                spot.z = location.z + z;
                spot.x = location.x + x;
                GameObject floor = Instantiate(floorPrefab, new Vector3(spot.x, spot.y, spot.z) * scale, Quaternion.identity);
                floor.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
                floor.transform.SetParent(parentObject.transform);
            }
        }
        //InstantiateRoomWalls(location, size, parentObject);


        return parentObject;

    }

    void InstantiateRoomWalls(Vector3Int location, Vector3Int size, GameObject parentObject)
    {
        for (int z = 0; z < size.z; z++)
        {
            Vector3Int checkDoorOne = new Vector3Int(location.x, location.y, location.z + z);
            Vector3Int checkHallOne = new Vector3Int(location.x - 1, location.y, location.z + z);
            if (isDoorway(checkDoorOne, checkHallOne))
            {
                InstantiateDoor(checkDoorOne, checkHallOne).transform.SetParent(parentObject.transform);
            }
            else
            {
                GameObject wallOne = Instantiate(wallPrefab, new Vector3(location.x, location.y, location.z + z) * scale, Quaternion.identity);
                wallOne.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
                var oneCenter = wallOne.GetComponent<Renderer>().bounds.center;
                oneCenter.z += scale / 2;
                oneCenter.x += scale / 2;
                wallOne.transform.RotateAround(oneCenter, Vector3.up, 90);
                wallOne.transform.SetParent(parentObject.transform);

            }

            Vector3Int checkDoorTwo = new Vector3Int(location.x + size.x - 1, location.y, location.z + z);
            Vector3Int checkHallTwo = new Vector3Int(location.x + size.x, location.z + z);
            if (isDoorway(checkDoorTwo, checkHallTwo))
            {
                InstantiateDoor(checkDoorTwo, checkHallTwo).transform.SetParent(parentObject.transform);
            }
            else
            {
                GameObject wallTwo = Instantiate(wallPrefab, new Vector3(location.x + size.x - 1, location.y, location.z + z) * scale, Quaternion.identity);
                wallTwo.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
                var twoCenter = wallTwo.GetComponent<Renderer>().bounds.center;
                twoCenter.z += scale / 2;
                twoCenter.x += scale / 2;
                wallTwo.transform.RotateAround(twoCenter, Vector3.up, -90);
                wallTwo.transform.SetParent(parentObject.transform);
            }
        }
        for (int x = 0; x < size.x; x++)
        {
            Vector3Int checkDoorOne = new Vector3Int(location.x + x, location.y, location.z);
            Vector3Int checkHallOne = new Vector3Int(location.x + x, location.y, location.z - 1);
            if (isDoorway(checkDoorOne, checkHallOne))
            {
                InstantiateDoor(checkDoorOne, checkHallOne).transform.SetParent(parentObject.transform);
            }
            else
            {
                GameObject wallOne = Instantiate(wallPrefab, new Vector3(location.x + x, location.y, location.z) * scale, Quaternion.identity);
                wallOne.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
                wallOne.transform.SetParent(parentObject.transform);
            }

            Vector3Int checkDoorTwo = new Vector3Int(location.x + x, location.y, location.z + size.z - 1);
            Vector3Int checkHallTwo = new Vector3Int(location.x + x, location.y, location.z + size.z);
            if (isDoorway(checkDoorTwo, checkHallTwo))
            {
                InstantiateDoor(checkDoorTwo, checkHallTwo).transform.SetParent(parentObject.transform);
            }
            else
            {

                GameObject wallTwo = Instantiate(wallPrefab, new Vector3(location.x + x, location.y, location.z + size.z - 1) * scale, Quaternion.identity);
                wallTwo.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
                var twoCenter = wallTwo.GetComponent<Renderer>().bounds.center;
                twoCenter.z += scale / 2;
                twoCenter.x += scale / 2;
                wallTwo.transform.RotateAround(twoCenter, Vector3.up, 180);
                wallTwo.transform.SetParent(parentObject.transform);
            }

        }

        parentObject.AddComponent<MeshRenderer>();

    }
    bool isDoorway(Vector3Int checkDoor, Vector3Int checkHall)
    {
        if (grid[checkDoor] != CellType.Door || checkHall.x < 0 || checkHall.x >= size.x || checkHall.y < 0 || checkHall.y >= size.y || grid[checkHall] != CellType.Hallway) return false;

        return true;
    }

    GameObject InstantiateHallwayPiece(Vector3Int location)
    {
        GameObject go = Instantiate(hallwayPrefab, new Vector3(location.x, location.y, location.z) * scale, Quaternion.identity);
        go.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
        return go;
    }


    GameObject InstantiateDoor(Vector3Int location, Vector3Int hallway)
    {
        Vector3 direction = (hallway - location);
        // Instantiate the door with correct orientation
        GameObject go = Instantiate(doorPrefab, new Vector3(location.x, location.y, location.z) * scale, Quaternion.identity);
        go.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
        go.name = "Doorway";

        var center = go.GetComponent<Renderer>().bounds.center;

        if (direction.x > 0)
        {
            go.transform.RotateAround(center, Vector3.up, 180);
            go.transform.Translate(new Vector3(-1, 0, -1) * scale);
        }
        if (direction.x < 0)
        {
            go.transform.RotateAround(center, Vector3.up, 0);
            go.transform.Translate(new Vector3(0, 0, 0) * scale);
        }
        if (direction.y > 0)
        {
            go.transform.Translate(new Vector3(-1, 0, 0) * scale);
            go.transform.RotateAround(center, Vector3.up, 90);
        }
        if (direction.y < 0)
        {

            go.transform.Translate(new Vector3(0, 0, -1) * scale);
            go.transform.RotateAround(center, Vector3.up, -90);


        }
        return go;

    }

    GameObject InstantiateHallwayWall(Vector3Int hallway, Vector3Int empty)
    {
        Vector3 direction = (empty - hallway);
        // Instantiate the door with correct orientation
        GameObject go = Instantiate(hallwayWallPrefab, new Vector3(hallway.x, hallway.y, hallway.z) * scale, Quaternion.identity);
        go.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
        go.name = "Hallway Wall";

        var center = go.GetComponent<Renderer>().bounds.center;

        if (direction.x > 0)
        {
            go.transform.RotateAround(center, Vector3.up, -90);
            go.transform.Translate(new Vector3(0, 0, -1) * scale);
        }
        if (direction.x < 0)
        {
            go.transform.RotateAround(center, Vector3.up, 90);
            go.transform.Translate(new Vector3(-1, 0, 0) * scale);
        }
        if (direction.y > 0)
        {
            go.transform.Translate(new Vector3(-1, 0, -1) * scale);
            go.transform.RotateAround(center, Vector3.up, 180);
        }
        if (direction.y < 0)
        {
            go.transform.RotateAround(center, Vector3.up, 0);
            go.transform.Translate(new Vector3(0, 0, 0) * scale);
        }
        return go;

    }


    void InstantiateStairs()
    {
        foreach (var stair in stairwayPieces)
        {
            InstantiateStairPiece(stair);
        }
    }
    GameObject InstantiateStairPiece(Vector3Int location)
    {
        GameObject go = Instantiate(stairwayPrefab, new Vector3(location.x, location.y, location.z) * scale, Quaternion.identity);
        go.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
        return go;
    }
}