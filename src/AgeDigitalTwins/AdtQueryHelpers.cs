using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AgeDigitalTwins.Exceptions;

namespace AgeDigitalTwins;

public static class AdtQueryHelpers
{
    public static string ConvertAdtQueryToCypher(string adtQuery, string graphName)
    {
        // Clean up the query from line breaks and extra spaces
        adtQuery = Regex.Replace(adtQuery, @"\s+", " ").Trim();

        // Prepare RETURN and LIMIT clauses
        string returnClause;
        var selectMatch = Regex.Match(
            adtQuery,
            @"SELECT (?:TOP\((?<limit>\d+)\) )?(?<projections>.+) FROM",
            RegexOptions.IgnoreCase
        );
        string limitClause;
        bool usesWildcard = false;
        if (selectMatch.Success)
        {
            limitClause = selectMatch.Groups["limit"].Success
                ? "LIMIT " + selectMatch.Groups["limit"].Value
                : string.Empty;
            returnClause = ProcessPropertyAccessors(
                selectMatch.Groups["projections"].Value,
                graphName
            );
            if (returnClause.Contains("COUNT()", StringComparison.OrdinalIgnoreCase))
            {
                returnClause = "COUNT(*)";
            }
            if (returnClause == "*")
            {
                usesWildcard = true;
            }
        }
        else
            throw new InvalidAdtQueryException("Invalid query format.");

        // Prepare MATCH clause
        string matchClause;
        // MultiLabel Edge WHERE clause
        List<string> multiLabelEdgeWhereClauses = new();
        if (adtQuery.Contains("FROM RELATIONSHIPS", StringComparison.OrdinalIgnoreCase))
        {
            // Handle RELATIONSHIPS source
            var match = Regex.Match(
                adtQuery,
                @"FROM RELATIONSHIPS (\w+)?(?=\s+WHERE|\s*$)",
                RegexOptions.IgnoreCase
            );
            if (match.Success)
            {
                var relationshipAlias = match.Groups[1].Value;
                matchClause = $"(:Twin)-[{relationshipAlias}]->(:Twin)";
            }
            else if (
                string.Equals(returnClause, "*")
                || string.Equals(returnClause, "COUNT(*)", StringComparison.OrdinalIgnoreCase)
            )
            {
                matchClause = "(:Twin)-[R]->(:Twin)";
            }
            else
                throw new InvalidAdtQueryException("Invalid query format.");
        }
        else if (adtQuery.Contains("FROM DIGITALTWINS", StringComparison.OrdinalIgnoreCase))
        {
            if (adtQuery.Contains("MATCH", StringComparison.OrdinalIgnoreCase))
            {
                // Handle MATCH clause
                var match = Regex.Match(
                    adtQuery,
                    @"FROM DIGITALTWINS MATCH (.+?)(?=\s+WHERE|\s*$)",
                    RegexOptions.IgnoreCase
                );
                if (match.Success)
                {
                    var adtMatchClause = match.Groups[1].Value;

                    // Add :Twin to all round brackets in the MATCH clause
                    matchClause = Regex.Replace(adtMatchClause, @"\((\w+)\)", "($1:Twin)");

                    // AGE currently doesn't support the pipe operator
                    // See https://github.com/apache/age/issues/1714
                    // There is an open PR to support this syntax https://github.com/apache/age/pull/2082
                    // Until then we need to use a workaround and generate something like this
                    if (matchClause.Contains('|'))
                    {
                        // In the MATCH clause every multi-label edge definition should be removed and converted to a WHERE clause
                        // [R:hasBlob|hasModel] -> label(R) = 'hasBlob' OR label(R) = 'hasModel'
                        // [R:hasBlob|hasModel|has] -> (label(R) = 'hasBlob' OR label(R) = 'hasModel' OR label(R) = 'has')
                        // (n1:Twin)-[r1:hasBlob|hasModel|has]->(n2)-[r2:contains|includes]->(n3) -> clause1: (label(r1) = 'hasBlob' OR label(r1) = 'hasModel' OR label(r1) = 'has') , clause2: (label(r2) = 'contains' OR label(r2) = 'includes')
                        // This also has to work for multiple matches in the same query
                        var multiLabelEdgeMatches = Regex.Matches(
                            matchClause,
                            @"\[(\w+):([\w\|]+)\]"
                        );
                        foreach (Match multiLabelEdgeMatch in multiLabelEdgeMatches)
                        {
                            var relationshipAlias = multiLabelEdgeMatch.Groups[1].Value;
                            var labels = multiLabelEdgeMatch.Groups[2].Value.Split('|').ToList();
                            var labelConditions = labels.Select(label =>
                                $"label({relationshipAlias}) = '{label}'"
                            );
                            multiLabelEdgeWhereClauses.Add(
                                $"({string.Join(" OR ", labelConditions)})"
                            );

                            // Remove the multi-label edge definition from the match clause
                            matchClause = matchClause.Replace(
                                multiLabelEdgeMatch.Value,
                                $"[{relationshipAlias}]"
                            );
                        }
                    }
                }
                else
                    throw new InvalidAdtQueryException("Invalid query format.");
            }
            else if (adtQuery.Contains("JOIN", StringComparison.OrdinalIgnoreCase))
            {
                var joinMatches = Regex.Matches(
                    adtQuery,
                    @"JOIN (\w+) RELATED (\w+)\.(\w+)(?: (\w+))?(?=\s+JOIN|\s+WHERE|\s*$)",
                    RegexOptions.IgnoreCase
                );
                List<string> matchClauses = new();

                foreach (Match joinMatch in joinMatches)
                {
                    var targetTwinAlias = joinMatch.Groups[1].Value;
                    var twinAlias = joinMatch.Groups[2].Value;
                    var relationshipName = joinMatch.Groups[3].Value;
                    var relationshipAlias = joinMatch.Groups[4].Success
                        ? joinMatch.Groups[4].Value
                        : string.Empty;

                    if (string.IsNullOrEmpty(relationshipAlias))
                    {
                        matchClauses.Add(
                            $"({twinAlias}:Twin)-[:{relationshipName}]->({targetTwinAlias}:Twin)"
                        );
                    }
                    else
                    {
                        matchClauses.Add(
                            $"({twinAlias}:Twin)-[{relationshipAlias}:{relationshipName}]->({targetTwinAlias}:Twin)"
                        );
                    }
                }

                if (matchClauses.Count == 0)
                    throw new InvalidAdtQueryException("Invalid query format.");

                matchClause = string.Join(",", matchClauses);
            }
            else
            {
                var match = Regex.Match(
                    adtQuery,
                    @"FROM DIGITALTWINS (\w+)?(?=\s+WHERE|\s*$)",
                    RegexOptions.IgnoreCase
                );
                if (match.Success)
                {
                    var twinAlias = match.Groups[1].Value;
                    matchClause = $"({twinAlias}:Twin)";
                }
                else if (
                    string.Equals(returnClause, "*")
                    || string.Equals(returnClause, "COUNT(*)", StringComparison.OrdinalIgnoreCase)
                )
                {
                    matchClause = "(T:Twin)";
                }
                else
                    throw new InvalidAdtQueryException("Invalid query format.");
            }
        }
        else
        {
            throw new InvalidAdtQueryException("Invalid query format.");
        }

        // Prepare WHERE clause
        string whereClause = string.Empty;
        if (adtQuery.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(adtQuery, @"WHERE (.+)");
            if (match.Success)
            {
                var adtWhereClause = match.Groups[1].Value;

                // Process WHERE clause
                whereClause = ProcessPropertyAccessors(
                    adtWhereClause,
                    graphName,
                    usesWildcard
                        && adtQuery.Contains(
                            "FROM RELATIONSHIPS",
                            StringComparison.OrdinalIgnoreCase
                        )
                            ? "R"
                        : usesWildcard ? "T"
                        : null
                );
            }
            else
                throw new InvalidAdtQueryException("Invalid query format.");
        }

        // Join everything together to form the final Cypher query
        string cypher = "MATCH " + matchClause;
        if (!string.IsNullOrEmpty(whereClause))
        {
            if (multiLabelEdgeWhereClauses.Count > 0)
            {
                cypher +=
                    " WHERE "
                    + string.Join(" AND ", multiLabelEdgeWhereClauses)
                    + " AND ("
                    + whereClause
                    + ")";
                ;
            }
            else
            {
                cypher += " WHERE " + whereClause;
            }
        }
        else if (multiLabelEdgeWhereClauses.Count > 0)
        {
            cypher += " WHERE " + string.Join(" AND ", multiLabelEdgeWhereClauses);
        }
        cypher += " RETURN " + returnClause;
        if (!string.IsNullOrEmpty(limitClause))
        {
            cypher += " " + limitClause;
        }
        return cypher;
    }

    internal static string ProcessPropertyAccessors(
        string whereClause,
        string graphName,
        string? prependAlias = null
    )
    {
        if (!string.IsNullOrEmpty(prependAlias))
        {
            // Handle function calls without prepending the alias to the function name
            whereClause = Regex.Replace(
                whereClause,
                @"(\w+)\(([^)]+)\)",
                m =>
                {
                    var functionName = m.Groups[1].Value;
                    var functionArgs = m.Groups[2].Value;

                    // Prepend alias to properties within the function arguments
                    functionArgs = Regex.Replace(
                        functionArgs,
                        @"(?<=\s|\[|^)(?!\d+|'[^']*'|""[^""]*"")[^\[\]""\s=<>!]+(?=\s*=\s*'|\s|$|\])",
                        n =>
                        {
                            return $"{prependAlias}.{n.Value}";
                        },
                        RegexOptions.IgnoreCase
                    );

                    return $"{functionName}({functionArgs})";
                },
                RegexOptions.IgnoreCase
            );

            // Prepend alias to properties outside of function calls
            whereClause = Regex.Replace(
                whereClause,
                @"(?<=\s|\[|^)(?!AND\b|OR\b|\d+|'[^']*'|""[^""]*"")[^\[\]""\s=<>!()]+(?=\s*=\s*'|\s|$|\])",
                m =>
                {
                    return $"{prependAlias}.{m.Value}";
                },
                RegexOptions.IgnoreCase
            );

            // Process IS_OF_MODEL function
            whereClause = Regex.Replace(
                whereClause,
                @"IS_OF_MODEL\(([^)]+)\)",
                m =>
                {
                    return $"{graphName}.is_of_model({prependAlias},{m.Groups[1].Value})";
                },
                RegexOptions.IgnoreCase
            );
        }
        else
        {
            // Process IS_OF_MODEL function without prepend alias
            whereClause = Regex.Replace(
                whereClause,
                @"IS_OF_MODEL\(([^)]+)\)",
                m =>
                {
                    return $"{graphName}.is_of_model({m.Groups[1].Value})";
                },
                RegexOptions.IgnoreCase
            );
        }

        // Process string function STARTSWITH
        whereClause = Regex.Replace(
            whereClause,
            @"STARTSWITH\(([^,]+),\s*'([^']+)'\)",
            m =>
            {
                return $"{m.Groups[1].Value} STARTS WITH '{m.Groups[2].Value}'";
            },
            RegexOptions.IgnoreCase
        );

        // Process string function ENDSWITH
        whereClause = Regex.Replace(
            whereClause,
            @"ENDSWITH\(([^,]+),\s*'([^']+)'\)",
            m =>
            {
                return $"{m.Groups[1].Value} ENDS WITH '{m.Groups[2].Value}'";
            },
            RegexOptions.IgnoreCase
        );

        // Process string function CONTAINS
        whereClause = Regex.Replace(
            whereClause,
            @"CONTAINS\(([^,]+),\s*'([^']+)'\)",
            m =>
            {
                return $"{m.Groups[1].Value} CONTAINS '{m.Groups[2].Value}'";
            },
            RegexOptions.IgnoreCase
        );

        // Process IS_NULL function
        whereClause = Regex.Replace(
            whereClause,
            @"IS_NULL\(([^)]+)\)",
            m =>
            {
                return $"{m.Groups[1].Value} IS NULL";
            },
            RegexOptions.IgnoreCase
        );

        // Process IS_DEFINED function
        whereClause = Regex.Replace(
            whereClause,
            @"IS_DEFINED\(([^)]+)\)",
            m =>
            {
                return $"{m.Groups[1].Value} IS NOT NULL";
            },
            RegexOptions.IgnoreCase
        );

        // Replace property access with $ character
        whereClause = Regex.Replace(whereClause, @"(\.\$[\w]+)", m => $"['{m.Value[1..]}']");

        // TODO: evaluate whether backticks would be better instead

        return whereClause;
    }
}
