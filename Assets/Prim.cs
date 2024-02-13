using System;
using System.Collections.Generic;
using UnityEngine;
using Graphs;

public static class Prim {
    public class Edge : Graphs.Edge {
        public float Distance { get; private set; }

        public Edge(Vertex u, Vertex v) : base(u, v) {
            Distance = Vector3.Distance(u.Position, v.Position);
        }

        public static bool operator ==(Edge left, Edge right)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            // If one is null, but not both, return false.
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            // Now that we've handled the null checks, proceed with the comparison.
            return (left.U == right.U && left.V == right.V) || (left.U == right.V && left.V == right.U);
        }

        public static bool operator !=(Edge left, Edge right) {
            return !(left == right);
        }

        public override bool Equals(object obj) {
            if (obj is Edge e) {
                return this == e;
            }

            return false;
        }

        public bool Equals(Edge e) {
            return this == e;
        }

        public override int GetHashCode() {
            return U.GetHashCode() ^ V.GetHashCode();
        }
    }


    public static List<Edge> MinimumSpanningTree(List<Edge> edges, Vertex start)
    {
        HashSet<Vertex> openSet = new HashSet<Vertex>();
        HashSet<Vertex> closedSet = new HashSet<Vertex>();

        // Initialize the open set with all vertices.
        foreach (var edge in edges)
        {
            openSet.Add(edge.U);
            openSet.Add(edge.V);
        }

        // Start with the specified start vertex.
        closedSet.Add(start);
        openSet.Remove(start);

        List<Edge> results = new List<Edge>();

        while (openSet.Count > 0)
        {
            Edge chosenEdge = null;
            float minWeight = float.PositiveInfinity;

            foreach (var edge in edges)
            {
                bool isUClosed = closedSet.Contains(edge.U);
                bool isVClosed = closedSet.Contains(edge.V);

                // Ensure one vertex is in the closed set and the other is in the open set.
                if (isUClosed ^ isVClosed)
                {
                    if (edge.Distance < minWeight)
                    {
                        chosenEdge = edge;
                        minWeight = edge.Distance;
                    }
                }
            }

            if (chosenEdge == null)
            {
                Debug.LogError("MST construction failed: Graph might be disconnected.");
                break;
            }

            // Add the chosen edge to the MST.
            results.Add(chosenEdge);

            // Move the newly connected vertex from open to closed set.
            if (closedSet.Contains(chosenEdge.U))
            {
                closedSet.Add(chosenEdge.V);
                openSet.Remove(chosenEdge.V);
            }
            else
            {
                closedSet.Add(chosenEdge.U);
                openSet.Remove(chosenEdge.U);
            }
        }

        if (openSet.Count > 0)
        {
            Debug.LogWarning("MST completed but not all vertices are connected. Open set is not empty.");
        }

        return results;
    }


}
