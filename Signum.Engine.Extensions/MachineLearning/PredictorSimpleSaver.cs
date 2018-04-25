﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Signum.Entities;
using Signum.Entities.MachineLearning;
using Signum.Utilities.Reflection;
using Signum.Utilities;
using Signum.Entities.DynamicQuery;
using Signum.Engine.Operations;

namespace Signum.Engine.MachineLearning
{
    public class PredictorSimpleSaver : IPredictorResultSaver
    {
        public bool SaveSimpleResults = false;

        public void AssertValid(PredictorEntity predictor)
        {
            AssertOnlyOutput(predictor);
        }

        public PredictorColumnEmbedded AssertOnlyOutput(PredictorEntity predictor)
        {
            var outputs = predictor.MainQuery.Columns.Where(a => a.Usage == PredictorColumnUsage.Output);
            if (outputs.Count() != 1)
                throw new InvalidOperationException($"{predictor.ResultSaver} requires the predictor to have only one output (instead of {outputs.Count()})");

            return outputs.SingleEx();
        }

        public void SavePredictions(PredictorTrainingContext ctx)
        {
            var p = ctx.Predictor.ToLite();
            var outputColumn = AssertOnlyOutput(ctx.Predictor);
            var isCategorical = outputColumn.Encoding == PredictorColumnEncoding.OneHot || outputColumn.Encoding == PredictorColumnEncoding.Codified;

            var keys = !ctx.Predictor.MainQuery.GroupResults ? null : ctx.Predictor.MainQuery.Columns.Where(c => !(c.Token.Token is AggregateToken)).ToList();
            var key0 = keys?.ElementAtOrDefault(0);
            var key1 = keys?.ElementAtOrDefault(1);
            var key2 = keys?.ElementAtOrDefault(2);

     
            ctx.ReportProgress($"Deleting old {typeof(PredictSimpleResultEntity).NicePluralName()}");
            {
                var query = Database.Query<PredictSimpleResultEntity>().Where(a => a.Predictor == p);
                int chunkSize = 5000;
                var totalCount = query.Count();
                var deleted = 0;
                while (totalCount - deleted > 0)
                {
                    int num = query.OrderBy(a => a.Id).Take(chunkSize).UnsafeDelete();
                    deleted += num;
                    ctx.ReportProgress($"Deleting old {typeof(PredictSimpleResultEntity).NicePluralName()}", deleted / (decimal)totalCount);
                }
            }

            ctx.ReportProgress($"Creating {typeof(PredictSimpleResultEntity).NicePluralName()}");
            {
                var dictionary = ctx.ToPredictDictionaries();
                var toInsert = new List<PredictSimpleResultEntity>();

                var pc = PredictorPredictLogic.CreatePredictContext(ctx.Predictor);
                int i = 0;
                foreach (var kvp in dictionary)
                {
                    if (i++ % 100 == 0)
                        ctx.ReportProgress($"Creating {typeof(PredictSimpleResultEntity).NicePluralName()}", i / (decimal)dictionary.Count);

                    var input = kvp.Value;

                    var output = pc.Algorithm.Predict(pc, input);

                    var inValue = input.MainQueryValues.GetOrThrow(outputColumn);
                    var outValue = output.MainQueryValues.GetOrThrow(outputColumn);
                    
                    toInsert.Add(new PredictSimpleResultEntity
                    {
                        Predictor = p,
                        Target = ctx.Predictor.MainQuery.GroupResults ? null : kvp.Key.Entity,
                        Type = ctx.Validation.Contains(kvp.Key) ? PredictionSet.Validation : PredictionSet.Training,
                        Key0 = key0 == null ? null : input.MainQueryValues.GetOrThrow(key0)?.ToString(),
                        Key1 = key1 == null ? null : input.MainQueryValues.GetOrThrow(key1)?.ToString(),
                        Key2 = key2 == null ? null : input.MainQueryValues.GetOrThrow(key2)?.ToString(),
                        OriginalValue = isCategorical ? null : ReflectionTools.ChangeType<double?>(inValue),
                        OriginalCategory = isCategorical ? inValue?.ToString() : null,
                        PredictedValue = isCategorical ? null : ReflectionTools.ChangeType<double?>(outValue),
                        PredictedCategory = isCategorical ? outValue?.ToString() : null,
                    });
                }

                ctx.Predictor.RegressionTraining = isCategorical ? null : GetRegressionStats(toInsert.Where(a => a.Type == PredictionSet.Training).ToList());
                ctx.Predictor.RegressionValidation = isCategorical ? null : GetRegressionStats(toInsert.Where(a => a.Type == PredictionSet.Validation).ToList());
                ctx.Predictor.ClassificationTraining = !isCategorical ? null : GetClassificationStats(toInsert.Where(a => a.Type == PredictionSet.Training).ToList());
                ctx.Predictor.ClassificationValidation = !isCategorical ? null : GetClassificationStats(toInsert.Where(a => a.Type == PredictionSet.Validation).ToList());
                using (OperationLogic.AllowSave<PredictorEntity>())
                    ctx.Predictor.Save();

                if (SaveSimpleResults)
                {
                    var groups = toInsert.GroupsOf(1000).ToList();
                    foreach (var iter in groups.Iterate())
                    {
                        ctx.ReportProgress($"Inserting {typeof(PredictSimpleResultEntity).NicePluralName()}", (iter.Position) / (decimal)groups.Count);
                        iter.Value.BulkInsert();
                    }
                }
            }
        }

        PredictorRegressionMetricsEmbedded GetRegressionStats(List<PredictSimpleResultEntity> list)
        {
            var mse = list.Average(p => Error(p) * Error(p));

            return new PredictorRegressionMetricsEmbedded
            {
                MeanError = list.Average(p => Error(p)).CleanDouble(),
                MeanAbsoluteError = list.Average(p => Math.Abs(Error(p))).CleanDouble(),
                MeanSquaredError = mse.CleanDouble(),
                RootMeanSquareError = Math.Sqrt(mse).CleanDouble(),
                MeanPercentageError = list.Average(p => SafeDiv(Error(p), p.OriginalValue.Value)).CleanDouble(),
                MeanAbsolutePercentageError = list.Average(p => Math.Abs(SafeDiv(Error(p), p.OriginalValue.Value))).CleanDouble(),
            };
        }

        double SafeDiv(double dividend, double divisor)
        {
            if (divisor == 0)
                return Math.Abs(dividend - divisor) < 0.0001 ? 0 : 1;

            return dividend / divisor;
        }

        double Error(PredictSimpleResultEntity p)
        {
            return p.PredictedValue.Value - p.OriginalValue.Value;
        }

        PredictorClassificationMetricsEmbedded GetClassificationStats(List<PredictSimpleResultEntity> list)
        {
            return new PredictorClassificationMetricsEmbedded
            {
                MissRate = list.Count(a => a.OriginalCategory != a.PredictedCategory),
                TotalCount = list.Count,
            };
        }
    }
}
