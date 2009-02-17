﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
namespace QuickGraph.Predicates
{
    [Serializable]
    public class FilteredImplicitVertexSet<TVertex, TEdge, TGraph> 
        : FilteredGraph<TVertex,TEdge,TGraph>
        , IImplicitVertexSet<TVertex>
        where TEdge : IEdge<TVertex>
        where TGraph : IGraph<TVertex, TEdge>, IImplicitVertexSet<TVertex>
    {
        public FilteredImplicitVertexSet(
            TGraph baseGraph,
            VertexPredicate<TVertex> vertexPredicate,
            EdgePredicate<TVertex, TEdge> edgePredicate
            )
            :base(baseGraph,vertexPredicate,edgePredicate)
        { }

        [Pure]
        public bool ContainsVertex(TVertex vertex)
        {
            if (!this.VertexPredicate(vertex))
                return false;
            return this.BaseGraph.ContainsVertex(vertex);
        }
    }
}