﻿#if EFCORE
using BlazarTech.QueryableValues.Builders;
using BlazarTech.QueryableValues.Serializers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Data;

namespace BlazarTech.QueryableValues.SqlServer
{
    internal sealed class JsonQueryableFactory : QueryableFactory
    {
        public JsonQueryableFactory(IJsonSerializer serializer, IDbContextOptions dbContextOptions)
            : base(serializer, dbContextOptions)
        {
        }

        protected override SqlParameter GetValuesParameter()
        {
            return new SqlParameter(null, SqlDbType.NVarChar, -1);
        }

        protected override string GetSql<TEntity>(IEntityOptionsBuilder entityOptions, bool useSelectTopOptimization, IReadOnlyList<EntityPropertyMapping> mappings)
        {
            var sb = StringBuilderPool.Get();

            try
            {
                if (useSelectTopOptimization)
                {
                    sb.Append(SqlSelectTop);
                }
                else
                {
                    sb.Append(SqlSelect);
                }

                sb.Append(" [").Append(QueryableValuesEntity.IndexPropertyName).Append(']');

                var collation = entityOptions.DefaultForCollation;
                if (!string.IsNullOrEmpty(collation))
                {
                    sb.Append(" COLLATE ").Append(collation);
                }

                foreach (var mapping in mappings)
                {
                    var propertyOptions = entityOptions.GetPropertyOptions(mapping.Source);
                    var propertyCollation = (propertyOptions?.Collation ?? entityOptions.DefaultForCollation);

                    sb.Append(", [").Append(mapping.Target.Name).Append(']');

                    if (!string.IsNullOrEmpty(propertyCollation))
                    {
                        sb.Append(" COLLATE ").Append(propertyCollation);
                    }
                }

                if (typeof(TEntity) == typeof(ComplexQueryableValuesEntity))
                {
                    // This is necessary because, in some cases, EF will render all the properties of TEntity
                    // in the outer parts of the query, regardless of the number of properties that were actually projected.
                    // This behavior was introduced in EF7+.
                    // See JoinWithProjection test for an example.
                    foreach (var unmappedPropertyName in ComplexQueryableValuesEntity.GetUnmappedPropertyNames(mappings))
                    {
                        sb.Append(", NULL[").Append(unmappedPropertyName).Append(']');
                    }
                }

                sb.AppendLine();
                sb.Append("FROM OPENJSON({0}) WITH ([").Append(QueryableValuesEntity.IndexPropertyName).Append("] int");

                foreach (var mapping in mappings)
                {
                    var propertyOptions = entityOptions.GetPropertyOptions(mapping.Source);

                    sb.Append(", ");

                    var targetName = mapping.Target.Name;

                    sb.Append('[').Append(targetName).Append("] ");

                    switch (mapping.TypeName)
                    {
                        case EntityPropertyTypeName.Boolean:
                            sb.Append("bit");
                            break;
                        case EntityPropertyTypeName.Byte:
                            sb.Append("tinyint");
                            break;
                        case EntityPropertyTypeName.Int16:
                            sb.Append("smallint");
                            break;
                        case EntityPropertyTypeName.Int32:
                            sb.Append("int");
                            break;
                        case EntityPropertyTypeName.Int64:
                            sb.Append("bigint");
                            break;
                        case EntityPropertyTypeName.Decimal:
                            {
                                var numberOfDecimals = propertyOptions?.NumberOfDecimals ?? entityOptions.DefaultForNumberOfDecimals;
                                sb.Append("decimal(38, ").Append(numberOfDecimals).Append(')');
                            }
                            break;
                        case EntityPropertyTypeName.Single:
                            sb.Append("real");
                            break;
                        case EntityPropertyTypeName.Double:
                            sb.Append("float");
                            break;
                        case EntityPropertyTypeName.DateTime:
                            sb.Append("datetime2");
                            break;
                        case EntityPropertyTypeName.DateTimeOffset:
                            sb.Append("datetimeoffset");
                            break;
                        case EntityPropertyTypeName.Guid:
                            sb.Append("uniqueidentifier");
                            break;
                        case EntityPropertyTypeName.Char:
                            if ((propertyOptions?.IsUnicode ?? entityOptions.DefaultForIsUnicode) == true)
                            {
                                sb.Append("nvarchar(1)");
                            }
                            else
                            {
                                sb.Append("varchar(1)");
                            }
                            break;
                        case EntityPropertyTypeName.String:
                            if ((propertyOptions?.IsUnicode ?? entityOptions.DefaultForIsUnicode) == true)
                            {
                                sb.Append("nvarchar(max)");
                            }
                            else
                            {
                                sb.Append("varchar(max)");
                            }
                            break;
#if EFCORE8
                        case EntityPropertyTypeName.DateOnly:
                            sb.Append("date");
                            break;
                        case EntityPropertyTypeName.TimeOnly:
                            sb.Append("time");
                            break;
#endif
                        default:
                            throw new NotImplementedException(mapping.TypeName.ToString());
                    }
                }

                sb.Append(')').AppendLine();
                sb.Append("ORDER BY [").Append(QueryableValuesEntity.IndexPropertyName).Append(']');

                return sb.ToString();
            }
            finally
            {
                StringBuilderPool.Return(sb);
            }
        }
    }
}
#endif