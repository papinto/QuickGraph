﻿using System;
using System.Collections.Generic;
using QuickGraph.Algorithms.Search;
using QuickGraph.Algorithms.Observers;
using QuickGraph.Collections;
using QuickGraph.Algorithms.Services;
using System.Diagnostics.Contracts;
using System.Diagnostics;

namespace QuickGraph.Algorithms.ShortestPath
{
    /// <summary>
    /// A* single-source shortest path algorithm for directed graph
    /// with positive distance.
    /// </summary>
    /// <typeparam name="Vertex"></typeparam>
    /// <typeparam name="Edge"></typeparam>
    /// <reference-ref
    ///     idref="lawler01combinatorial"
    ///     />
    [Serializable]
    public sealed class AStarShortestPathAlgorithm<TVertex, TEdge> :
        ShortestPathAlgorithmBase<TVertex, TEdge, IVertexListGraph<TVertex, TEdge>>,
        IVertexColorizerAlgorithm<TVertex, TEdge>,
        IVertexPredecessorRecorderAlgorithm<TVertex, TEdge>,
        IDistanceRecorderAlgorithm<TVertex, TEdge>
        where TEdge : IEdge<TVertex>
    {
        private FibonacciQueue<TVertex, double> vertexQueue;
        private Dictionary<TVertex, double> costs;
        private readonly Func<TVertex, double> costHeuristic;

        public AStarShortestPathAlgorithm(
            IVertexListGraph<TVertex, TEdge> visitedGraph,
            Func<TEdge, double> weights,
            Func<TVertex, double> costHeuristic
            )
            : this(visitedGraph, weights, costHeuristic, ShortestDistanceRelaxer.Instance)
        { }

        public AStarShortestPathAlgorithm(
            IVertexListGraph<TVertex, TEdge> visitedGraph,
            Func<TEdge, double> weights,
            Func<TVertex, double> costHeuristic,
            IDistanceRelaxer distanceRelaxer
            )
            : this(null, visitedGraph, weights, costHeuristic, distanceRelaxer)
        { }

        public AStarShortestPathAlgorithm(
            IAlgorithmComponent host,
            IVertexListGraph<TVertex, TEdge> visitedGraph,
            Func<TEdge, double> weights,
            Func<TVertex, double> costHeuristic,
            IDistanceRelaxer distanceRelaxer
            )
            : base(host, visitedGraph, weights, distanceRelaxer)
        {
            Contract.Requires(costHeuristic != null);

            this.costHeuristic = costHeuristic;
        }

        public Func<TVertex, double> CostHeuristic
        {
            get { return this.costHeuristic; }
        }

        public event VertexEventHandler<TVertex> InitializeVertex;
        public event VertexEventHandler<TVertex> DiscoverVertex;
        public event VertexEventHandler<TVertex> StartVertex;
        public event VertexEventHandler<TVertex> ExamineVertex;
        public event EdgeEventHandler<TVertex, TEdge> ExamineEdge;
        public event VertexEventHandler<TVertex> FinishVertex;

        public event EdgeEventHandler<TVertex, TEdge> EdgeNotRelaxed;
        private void OnEdgeNotRelaxed(TEdge e)
        {
            var eh = this.EdgeNotRelaxed;
            if (eh != null)
                eh(this, new EdgeEventArgs<TVertex, TEdge>(e));
        }

        private void InternalExamineEdge(Object sender, EdgeEventArgs<TVertex, TEdge> args)
        {
            if (this.Weights(args.Edge) < 0)
                throw new NegativeWeightException();
        }

        private void InternalTreeEdge(Object sender, EdgeEventArgs<TVertex, TEdge> args)
        {
            bool decreased = this.Relax(args.Edge);
            if (decreased)
            {
                this.OnTreeEdge(args.Edge);
                this.AssertHeap();
            }
            else
                this.OnEdgeNotRelaxed(args.Edge);
        }

        private void InternalGrayTarget(Object sender, EdgeEventArgs<TVertex, TEdge> args)
        {
            var e = args.Edge;
            var target = e.Target;

            bool decreased = this.Relax(e);
            double distance = this.Distances[target];
            if (decreased)
            {
                this.costs[target] = this.DistanceRelaxer.Combine(distance, this.costHeuristic(target));
                this.vertexQueue.Update(target);
                this.AssertHeap();
                this.OnTreeEdge(args.Edge);
            }
            else
            {
                this.OnEdgeNotRelaxed(args.Edge);
            }
        }

        private void InternalBlackTarget(Object sender, EdgeEventArgs<TVertex, TEdge> args)
        {
            var e = args.Edge;
            var target = e.Target;

            bool decreased = this.Relax(e);
            double distance = this.Distances[target];
            if (decreased)
            {
                this.OnTreeEdge(args.Edge);
                this.costs[target] = this.DistanceRelaxer.Combine(distance, this.costHeuristic(target));
                this.vertexQueue.Enqueue(target);
                this.AssertHeap();
                this.VertexColors[target] = GraphColor.Gray;
            }
            else
            {
                this.OnEdgeNotRelaxed(args.Edge);
            }
        }

        protected override void Initialize()
        {
            base.Initialize();

            this.VertexColors.Clear();
            this.costs = new Dictionary<TVertex, double>(this.VisitedGraph.VertexCount);
            // init color, distance
            var initialDistance = this.DistanceRelaxer.InitialDistance;
            foreach (var u in VisitedGraph.Vertices)
            {
                this.VertexColors.Add(u, GraphColor.White);
                this.Distances.Add(u, initialDistance);
                this.costs.Add(u, initialDistance);
            }
            this.vertexQueue = new FibonacciQueue<TVertex, double>(this.costs);
        }

        protected override void InternalCompute()
        {
            TVertex rootVertex;
            if (this.TryGetRootVertex(out rootVertex))
                this.ComputeFromRoot(rootVertex);
            else
            {
                foreach (var v in this.VisitedGraph.Vertices)
                    if (this.VertexColors[v] == GraphColor.White)
                        this.ComputeFromRoot(v);
            }
        }

        private void ComputeFromRoot(TVertex rootVertex)
        {
            Contract.Requires(rootVertex != null);
            Contract.Requires(this.VisitedGraph.ContainsVertex(rootVertex));
            Contract.Requires(this.VertexColors[rootVertex] == GraphColor.White);

            this.VertexColors[rootVertex] = GraphColor.Gray;
            this.Distances[rootVertex] = 0;
            this.ComputeNoInit(rootVertex);
        }

        [Conditional("DEBUG")]
        private void AssertHeap()
        {
            if (this.vertexQueue.Count == 0) return;
            var top = this.vertexQueue.Peek();
            var vertices = this.vertexQueue.ToArray();
            for (int i = 1; i < vertices.Length; ++i)
                if (this.Distances[top] > this.Distances[vertices[i]])
                    Contract.Assert(false);
        }

        public void ComputeNoInit(TVertex s)
        {
            BreadthFirstSearchAlgorithm<TVertex, TEdge> bfs = null;

            try
            {
                bfs = new BreadthFirstSearchAlgorithm<TVertex, TEdge>(
                    this,
                    this.VisitedGraph,
                    this.vertexQueue,
                    VertexColors
                    );

                bfs.InitializeVertex += this.InitializeVertex;
                bfs.DiscoverVertex += this.DiscoverVertex;
                bfs.StartVertex += this.StartVertex;
                bfs.ExamineEdge += this.ExamineEdge;
#if DEBUG
                bfs.ExamineEdge += (sender, e) => this.AssertHeap();
#endif
                bfs.ExamineVertex += this.ExamineVertex;
                bfs.FinishVertex += this.FinishVertex;

                bfs.ExamineEdge += new EdgeEventHandler<TVertex, TEdge>(this.InternalExamineEdge);
                bfs.TreeEdge += new EdgeEventHandler<TVertex, TEdge>(this.InternalTreeEdge);
                bfs.GrayTarget += new EdgeEventHandler<TVertex, TEdge>(this.InternalGrayTarget);
                bfs.BlackTarget +=new EdgeEventHandler<TVertex,TEdge>(this.InternalBlackTarget);

                bfs.Visit(s);
            }
            finally
            {
                if (bfs != null)
                {
                    bfs.InitializeVertex -= this.InitializeVertex;
                    bfs.DiscoverVertex -= this.DiscoverVertex;
                    bfs.StartVertex -= this.StartVertex;
                    bfs.ExamineEdge -= this.ExamineEdge;
                    bfs.ExamineVertex -= this.ExamineVertex;
                    bfs.FinishVertex -= this.FinishVertex;

                    bfs.ExamineEdge -= new EdgeEventHandler<TVertex, TEdge>(this.InternalExamineEdge);
                    bfs.TreeEdge -= new EdgeEventHandler<TVertex, TEdge>(this.InternalTreeEdge);
                    bfs.GrayTarget -= new EdgeEventHandler<TVertex, TEdge>(this.InternalGrayTarget);
                    bfs.BlackTarget -= new EdgeEventHandler<TVertex, TEdge>(this.InternalBlackTarget);
                }
            }
        }
    }
}
