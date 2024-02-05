using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Graphs;
using System.Linq;
using System;

public class Generator2D : MonoBehaviour {
    enum CellType {
        None,
        Room,
        Hallway,
        Door
    }

    class Room {
        public RectInt bounds;

        public Room(Vector2Int location, Vector2Int size) {
            bounds = new RectInt(location, size);
        }

        public static bool Intersect(Room a, Room b) {
            return !((a.bounds.position.x >= (b.bounds.position.x + b.bounds.size.x)) || ((a.bounds.position.x + a.bounds.size.x) <= b.bounds.position.x)
                || (a.bounds.position.y >= (b.bounds.position.y + b.bounds.size.y)) || ((a.bounds.position.y + a.bounds.size.y) <= b.bounds.position.y));
        }
    }

    [SerializeField]
    private int seed;

    [SerializeField]
    private float scale = 1;
    [SerializeField]
    GameObject Player;

    [SerializeField]
    Vector2Int size;
    [SerializeField]
    int roomCount;
    [SerializeField]
    Vector2Int roomMaxSize;
    [SerializeField]
    GameObject floorPrefab;

    [SerializeField]
    GameObject hallwayPrefab;

    Grid2D<CellType> grid;
    List<Room> rooms;
    Delaunay2D delaunay;
    HashSet<Prim.Edge> selectedEdges;


 

    List<Vector2Int> hallwayPieces = new();
    [SerializeField]
    GameObject wallPrefab;
    [SerializeField]
    GameObject doorPrefab;
    [SerializeField]
    GameObject hallwayWallPrefab;

    [SerializeField]
    GameObject startingRoom;

    [SerializeField]
    double loopRate;


    void Start() {
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
        grid = new Grid2D<CellType>(size, Vector2Int.zero);
        rooms = new List<Room>();
        Generate();
    }
   
    void Generate() {


        Generator();
        Instantiator();

        //Debug.Log(Player.transform.position);
        var startPos = startingRoom.transform.position;
        //startPos.y += scale;
        startPos.x += scale/2;
        startPos.z += scale/2;
        Player.transform.position = startPos;
       // Debug.Log(Player.transform.position);
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

    }

    void GenerateRooms() {
        for (int i = 0; i < roomCount; i++) {
            Vector2Int location = new Vector2Int(
            UnityEngine.Random.Range(0, size.x),
            UnityEngine.Random.Range(0, size.y)
            );

            Vector2Int roomSize = new Vector2Int(
                UnityEngine.Random.Range(1, roomMaxSize.x + 1),
                UnityEngine.Random.Range(1, roomMaxSize.y + 1)
            );

            bool add = true;
            Room newRoom = new Room(location, roomSize);
            Room buffer = new Room(location + new Vector2Int(-1, -1), roomSize + new Vector2Int(2, 2));

            foreach (var room in rooms) {
                if (Room.Intersect(room, buffer)) {
                    add = false;
                    break;
                }
            }

            if (newRoom.bounds.xMin < 0 || newRoom.bounds.xMax >= size.x
                || newRoom.bounds.yMin < 0 || newRoom.bounds.yMax >= size.y) {
                add = false;
            }

            if (add) {
                rooms.Add(newRoom);
                foreach (var pos in newRoom.bounds.allPositionsWithin) {
                    grid[pos] = CellType.Room;
                }
            }
        }
    }

    void Triangulate() {
        List<Vertex> vertices = new List<Vertex>();

        foreach (var room in rooms) {
            vertices.Add(new Vertex<Room>((Vector2)room.bounds.position + ((Vector2)room.bounds.size) / 2, room));
        }

        delaunay = Delaunay2D.Triangulate(vertices);
    }

    void GenerateHallways() {
        List<Prim.Edge> edges = new List<Prim.Edge>();

        foreach (var edge in delaunay.Edges) {
            edges.Add(new Prim.Edge(edge.U, edge.V));
        }

        List<Prim.Edge> mst = Prim.MinimumSpanningTree(edges, edges[0].U);

        selectedEdges = new HashSet<Prim.Edge>(mst);
        var remainingEdges = new HashSet<Prim.Edge>(edges);
        remainingEdges.ExceptWith(selectedEdges);

        foreach (var edge in remainingEdges) {
            if (UnityEngine.Random.value < loopRate) {
                selectedEdges.Add(edge);
            }
        }
    }

    void PathfindHallways()
    {
        DungeonPathfinder2D aStar = new DungeonPathfinder2D(size);

        foreach (var edge in selectedEdges)
        {

            var startRoom = (edge.U as Vertex<Room>).Item;
            var endRoom = (edge.V as Vertex<Room>).Item;

            var startPosf = startRoom.bounds.center;
            var endPosf = endRoom.bounds.center;
            var startPos = new Vector2Int((int)startPosf.x, (int)startPosf.y);
            var endPos = new Vector2Int((int)endPosf.x, (int)endPosf.y);

            var path = aStar.FindPath(startPos, endPos, (DungeonPathfinder2D.Node a, DungeonPathfinder2D.Node b) => {
                var pathCost = new DungeonPathfinder2D.PathCost();

                pathCost.cost = Vector2Int.Distance(b.Position, endPos);    //heuristic

                if (grid[b.Position] == CellType.Room)
                {
                    pathCost.cost += 10;
                }
                else if (grid[b.Position] == CellType.None)
                {
                    pathCost.cost += 5;
                }
                else if (grid[b.Position] == CellType.Hallway)
                {
                    pathCost.cost += 1;
                }

                pathCost.traversable = true;

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


                        if (grid[current] == CellType.Hallway && grid[prev] == CellType.Room) 
                        {
                            grid[prev] = CellType.Door;

                        }
                        else if (grid[current] == CellType.Room && grid[prev]== CellType.Hallway)
                        {
                            grid[current]= CellType.Door; //PlaceDoor(current, prev); was original thought

                        }


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
        HashSet<Vector2Int> visited = new();
        Stack<Vector2Int> traverseHallway = new();
        foreach( var check in hallwayPieces)
        {
            if(!visited.Contains(check))
            {
                traverseHallway.Push(check);
                GameObject Parent = new GameObject("Hallway");

                while(traverseHallway.Count > 0) { 
                    var hallwayPiece = traverseHallway.Pop();
                    
                    visited.Add(hallwayPiece);
                    GameObject go = InstantiateHallwayPiece(hallwayPiece);
                    go.transform.parent = Parent.transform;
                    foreach (var neighbor in GetNeighbors(hallwayPiece))
                    {

                        if (neighbor.x < 0 || neighbor.x >= size.x || neighbor.y < 0 || neighbor.y >= size.y || grid[neighbor]== CellType.None)
                        {
                            InstantiateHallwayWall(hallwayPiece, neighbor);
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

    List<Vector2Int> GetNeighbors(Vector2Int piece)
    {
        return new List<Vector2Int>{
        new Vector2Int(piece.x+1, piece.y),
        new Vector2Int(piece.x-1, piece.y),
        new Vector2Int(piece.x, piece.y+1),
        new Vector2Int(piece.x, piece.y-1),
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



    GameObject InstantiateRoom(Vector2Int location, Vector2Int size)
    {   
        var spot = location;
        GameObject parentObject = new GameObject("Room");
        parentObject.transform.position = new Vector3(spot.x,0,spot.y)*scale;
        for (int y = 0; y < size.y; y++)
        {
            for(int x =0; x < size.x; x++)
            {

                spot.y = location.y + y;
                spot.x = location.x + x;
                GameObject floor = Instantiate(floorPrefab, new Vector3(spot.x, 0, spot.y)*scale, Quaternion.identity);
                floor.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
                floor.transform.SetParent(parentObject.transform);
            }
        }
        InstantiateRoomWalls(location, size, parentObject);
        
      
        return parentObject;
        
    }

    void InstantiateRoomWalls(Vector2Int location, Vector2Int size, GameObject parentObject)
    {
        for (int y = 0; y < size.y; y++)
        {
            Vector2Int checkDoorOne = new Vector2Int(location.x, location.y + y);
            Vector2Int checkHallOne = new Vector2Int(location.x-1, location.y + y);
            if (isDoorway(checkDoorOne, checkHallOne))
            {
                InstantiateDoor(checkDoorOne, checkHallOne).transform.SetParent(parentObject.transform);
            }
            else
            {
                GameObject wallOne = Instantiate(wallPrefab, new Vector3(location.x, 0, location.y + y) * scale, Quaternion.identity);
                wallOne.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
                var oneCenter = wallOne.GetComponent<Renderer>().bounds.center;
                oneCenter.z += scale / 2;
                oneCenter.x += scale / 2;
                wallOne.transform.RotateAround(oneCenter, Vector3.up, 90);
                wallOne.transform.SetParent(parentObject.transform);

            }

            Vector2Int checkDoorTwo = new Vector2Int(location.x + size.x - 1, location.y + y);
            Vector2Int checkHallTwo = new Vector2Int(location.x + size.x, location.y + y);
            if (isDoorway(checkDoorTwo, checkHallTwo))
            {
                InstantiateDoor(checkDoorTwo, checkHallTwo).transform.SetParent(parentObject.transform);
            }
            else
            {
                GameObject wallTwo = Instantiate(wallPrefab, new Vector3(location.x + size.x - 1, 0, location.y + y) * scale, Quaternion.identity);
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
            Vector2Int checkDoorOne = new Vector2Int(location.x + x, location.y);
            Vector2Int checkHallOne = new Vector2Int(location.x + x, location.y-1);
            if (isDoorway(checkDoorOne, checkHallOne))
            {
                InstantiateDoor(checkDoorOne, checkHallOne).transform.SetParent(parentObject.transform);
            }
            else
            {
                GameObject wallOne = Instantiate(wallPrefab, new Vector3(location.x + x, 0, location.y) * scale, Quaternion.identity);
                wallOne.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
                wallOne.transform.SetParent(parentObject.transform);
            }

            Vector2Int checkDoorTwo = new Vector2Int(location.x + x, location.y + size.y - 1);
            Vector2Int checkHallTwo = new Vector2Int(location.x + x, location.y + size.y);
            if (isDoorway(checkDoorTwo, checkHallTwo))
            {
                InstantiateDoor(checkDoorTwo, checkHallTwo).transform.SetParent(parentObject.transform);
            }
            else
            {

                GameObject wallTwo = Instantiate(wallPrefab, new Vector3(location.x + x, 0, location.y + size.y - 1) * scale, Quaternion.identity);
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
    bool isDoorway(Vector2Int checkDoor, Vector2Int checkHall)
    {
        if (grid[checkDoor] != CellType.Door || checkHall.x < 0 || checkHall.x >= size.x || checkHall.y < 0 || checkHall.y >= size.y || grid[checkHall] != CellType.Hallway) return false;

        return true;
    }


    GameObject InstantiateHallwayPiece(Vector2Int location)
    {
        GameObject go = Instantiate(hallwayPrefab, new Vector3(location.x, 0, location.y) * scale, Quaternion.identity);
        go.GetComponent<Transform>().localScale = new Vector3(1, 1, 1) * scale;
        return go;
    }

    GameObject InstantiateDoor(Vector2Int location, Vector2Int hallway)
    {
        Vector2 direction = (hallway- location);
        // Instantiate the door with correct orientation
        GameObject go = Instantiate(doorPrefab, new Vector3(location.x, 0, location.y) * scale,Quaternion.identity);
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
            go.transform.Translate(new Vector3(0,0,0)*scale);
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
    GameObject InstantiateHallwayWall(Vector2Int hallway, Vector2Int empty)
    {
        Vector2 direction = (empty - hallway);
        // Instantiate the door with correct orientation
        GameObject go = Instantiate(hallwayWallPrefab, new Vector3(hallway.x, 0, hallway.y) * scale, Quaternion.identity);
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

    GameObject InstantiateCube(Vector2Int location, Vector2Int size)
    {
        GameObject go = Instantiate(floorPrefab, new Vector3(location.x, 0, location.y) * scale, Quaternion.identity);
        go.GetComponent<Transform>().localScale = new Vector3(size.x, 1, size.y) * scale;
        return go;
    }

}