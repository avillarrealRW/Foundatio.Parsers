﻿using System;
using System.Linq;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Parsers.ElasticQueries.Query.Nodes;
using Foundatio.Parsers.LuceneQueries.Extensions;
using Nest;

namespace Foundatio.Parsers.ElasticQueries.Query {
    public class QueryContainerVisitor : ElasticQueryNodeVisitorBase {
        private readonly ElasticQueryParserConfiguration _config;

        public QueryContainerVisitor(ElasticQueryParserConfiguration config) {
            _config = config;
        }

        public override void Visit(QueryGroupNode node) {
            QueryContainer query = null;
            foreach (var child in node.Children.OfType<IElasticQueryNode>()) {
                child.Accept(this);

                var childQuery = child.Query;
                var op = node.GetOperator(_config.DefaultQueryOperator);
                if (child.IsNegated())
                    childQuery = !childQuery;

                if (op == Operator.Or && !String.IsNullOrEmpty(node.Prefix) && node.Prefix == "+")
                    op = Operator.And;

                if (op == Operator.And) {
                    query &= childQuery;
                } else if (op == Operator.Or) {
                    query |= childQuery;
                }
            }

            node.Query = query;
        }

        public override void Visit(QueryTermNode node) {
            PlainQuery query = null;
            if (_config.IsFieldAnalyzed(node.GetFullName())) {
                if (!node.IsQuotedTerm && node.UnescapedTerm.EndsWith("*")) {
                    query = new QueryStringQuery {
                        DefaultField = node.GetFullName() ?? _config.DefaultField,
                        AllowLeadingWildcard = false,
                        AnalyzeWildcard = true,
                        Query = node.UnescapedTerm
                    };
                } else {
                    var q = new MatchQuery {
                        Field = node.GetFullName() ?? _config.DefaultField,
                        Query = node.UnescapedTerm
                    };
                    if (node.IsQuotedTerm)
                        q.Type = "phrase";

                    query = q;
                }
            } else {
                query = new TermQuery {
                    Field = node.GetFullName(),
                    Value = node.UnescapedTerm
                };
            }

            node.Query = query;
        }

        public override void Visit(QueryTermRangeNode node) {
            var range = new RangeQuery { Field = node.GetFullName() };
            if (!String.IsNullOrWhiteSpace(node.UnescapedMin)) {
                if (node.MinInclusive.HasValue && !node.MinInclusive.Value)
                    range.GreaterThan = node.UnescapedMin;
                else
                    range.GreaterThanOrEqualTo = node.UnescapedMin;
            }

            if (!String.IsNullOrWhiteSpace(node.UnescapedMax)) {
                if (node.MaxInclusive.HasValue && !node.MaxInclusive.Value)
                    range.LowerThan = node.UnescapedMax;
                else
                    range.LowerThanOrEqualTo = node.UnescapedMax;
            }

            node.Query = range;
        }

        public override void Visit(QueryExistsNode node) {
            node.Query = new FilteredQuery {
                Filter = new ExistsFilter { Field = node.GetFullName() }.ToContainer()
            };
        }

        public override void Visit(QueryMissingNode node) {
            node.Query = new FilteredQuery {
                Filter = new MissingFilter { Field = node.GetFullName() }.ToContainer()
            };
        }
    }
}
