﻿using Signum.Entities;
using Signum.Entities.DynamicQuery;
using Signum.Entities.MachineLearning;
using Signum.Entities.UserAssets;
using Signum.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Signum.Engine.MachineLearning
{
    public class TrainingProgress
    {
        public string Message;
        public decimal? Progress;
        public bool Running;

        public PredictorState State { get; set; }

        public List<object[]> EpochProgresses { get; set; }
    }

    public class EpochProgress
    {
        public long Ellapsed;
        public int TrainingExamples;
        public int Epoch;
        public double? LossTraining;
        public double? EvaluationTraining;
        public double? LossValidation;
        public double? EvaluationValidation;

        object[] array;
        public object[] ToObjectArray()
        {
            return array ?? (array = new object[]
            {
                Ellapsed,
                TrainingExamples,
                Epoch,
                LossTraining,
                EvaluationTraining,
                LossValidation,
                EvaluationValidation,
            });
        }

        public PredictorEpochProgressEntity SaveEntity(PredictorEntity predictor)
        {
            return new PredictorEpochProgressEntity
            {
                Predictor = predictor.ToLite(),
                Ellapsed = Ellapsed,
                Epoch = Epoch,
                TrainingExamples = TrainingExamples,
                LossTraining = LossTraining?.CleanDouble(),
                EvaluationTraining = EvaluationTraining?.CleanDouble(),
                LossValidation = LossValidation?.CleanDouble(),
                EvaluationValidation = EvaluationValidation?.CleanDouble(),
            }.Save();
        }


    }

    public class PredictorPredictContext
    {
        public IPredictorAlgorithm Algorithm { get; }

        public PredictorEntity Predictor { get; }

        public List<PredictorCodification> Codifications { get; }
        public List<PredictorCodification> InputCodifications { get; }
        public Dictionary<PredictorColumnBase, List<PredictorCodification>> InputCodificationsByColumn { get; }

        public List<PredictorCodification> OutputCodifications { get; }
        public Dictionary<PredictorColumnEmbedded, List<PredictorCodification>> MainOutputCodifications { get; }
        public Dictionary<PredictorSubQueryEntity, PredictorPredictSubQueryContext> SubQueryOutputCodifications { get; }

        public object Model { get; set; }

        public PredictorPredictContext(PredictorEntity predictor, IPredictorAlgorithm algorithm, List<PredictorCodification> codifications)
        {
            Predictor = predictor;
            Algorithm = algorithm;
            Codifications = codifications;

            InputCodifications = codifications.Where(a => a.Column.Usage == PredictorColumnUsage.Input).ToList();
            InputCodificationsByColumn = InputCodifications.GroupToDictionary(a => a.Column);

            OutputCodifications = codifications.Where(a => a.Column.Usage == PredictorColumnUsage.Output).ToList();
            MainOutputCodifications = OutputCodifications.Where(a => a.Column is PredictorColumnMain m).GroupToDictionary(a => ((PredictorColumnMain)a.Column).PredictorColumn);
            SubQueryOutputCodifications = OutputCodifications.Where(a => a.Column is PredictorColumnSubQuery).AgGroupToDictionary(a => ((PredictorColumnSubQuery)a.Column).SubQuery, sqGroup => new PredictorPredictSubQueryContext
            {
                SubQuery = sqGroup.Key,
                Groups = sqGroup.AgGroupToDictionary(a => ((PredictorColumnSubQuery)a.Column).Keys,
                    keysGroup => keysGroup.GroupToDictionary(a => ((PredictorColumnSubQuery)a.Column).PredictorSubQueryColumn), ObjectArrayComparer.Instance)
            });
        }
    }

    public class PredictorPredictSubQueryContext
    {
        public PredictorSubQueryEntity SubQuery;
        public Dictionary<object[], Dictionary<PredictorSubQueryColumnEmbedded, List<PredictorCodification>>> Groups;
    }

    public class PredictorTrainingContext
    {
        public PredictorEntity Predictor { get; set; }
        public CancellationToken CancellationToken { get; }
        public bool StopTraining { get; set; }

        public string Message { get; set; }
        public decimal? Progress { get; set; }

        public List<PredictorCodification> Codifications { get; private set; }

        public List<PredictorCodification> InputCodifications { get; private set; }
        public Dictionary<PredictorColumnBase, List<PredictorCodification>> InputCodificationsByColumn { get; private set; }

        public List<PredictorCodification> OutputCodifications { get; private set; }
        public Dictionary<PredictorColumnBase, List<PredictorCodification>> OutputCodificationsByColumn { get; private set; }

        public List<ResultRow> Validation { get; internal set; }

        public MainQuery MainQuery { get; internal set; }
        public Dictionary<PredictorSubQueryEntity, SubQuery> SubQueries { get; internal set; }

        public ConcurrentQueue<EpochProgress> Progresses = new ConcurrentQueue<EpochProgress>();

        public PredictorTrainingContext(PredictorEntity predictor, CancellationToken cancellationToken)
        {
            this.Predictor = predictor;
            this.CancellationToken = cancellationToken;
        }

        public event Action<string, decimal?> OnReportProgres;

        public void ReportProgress(string message, decimal? progress = null)
        {
            this.CancellationToken.ThrowIfCancellationRequested();

            this.Message = message;
            this.Progress = progress;
            this.OnReportProgres?.Invoke(message, progress);
        }


        public void SetCodifications(PredictorCodification[] codifications)
        {
            this.Codifications = codifications.ToList();

            this.InputCodifications = codifications.Where(a => a.Column.Usage == PredictorColumnUsage.Input).ToList();
            this.InputCodificationsByColumn = this.InputCodifications.GroupToDictionary(a => a.Column);
            for (int i = 0; i < this.InputCodifications.Count; i++)
            {
                this.InputCodifications[i].Index = i;
            }


            this.OutputCodifications = codifications.Where(a => a.Column.Usage == PredictorColumnUsage.Output).ToList();
            this.OutputCodificationsByColumn = this.OutputCodifications.GroupToDictionary(a => a.Column);
            for (int i = 0; i < this.OutputCodifications.Count; i++)
            {
                this.OutputCodifications[i].Index = i;
            }
        }

        public (List<ResultRow> training, List<ResultRow> validation) SplitTrainValidation(Random r)
        {
            List<ResultRow> training = new List<ResultRow>();
            List<ResultRow> validation = new List<ResultRow>();

            foreach (var item in this.MainQuery.ResultTable.Rows)
            {
                if (r.NextDouble() < Predictor.Settings.TestPercentage)
                    validation.Add(item);
                else
                    training.Add(item);
            }

            this.Validation = validation;

            return (training, validation);
        }

        public List<object[]> GetProgessArray()
        {
            return Progresses.Select(a => a.ToObjectArray()).ToList();
        }
    }

    public class MainQuery
    {
        public QueryRequest QueryRequest { get; internal set; }
        public ResultTable ResultTable { get; internal set; }
        public Func<ResultRow, object[]> GetParentKey { get; internal set; }
    }

    public class SubQuery
    {
        public PredictorSubQueryEntity SubQueryEntity;
        public QueryRequest QueryGroupRequest;
        public ResultTable ResultTable;
        public Dictionary<object[], Dictionary<object[], object[]>> GroupedValues;


        public ResultColumn[] SplitBy { get; internal set; }
        public ResultColumn[] ValueColumns { get; internal set; }
        //From ColumnIndex (i.e: [3->0, 4->1)
        public Dictionary<int, int> ColumnIndexToValueIndex { get; internal set; }
    }

    public interface IPredictorAlgorithm
    {
        string ValidateEncodingProperty(PredictorEntity predictor, PredictorSubQueryEntity subQuery, PredictorColumnEncodingSymbol encoding, PredictorColumnUsage usage, QueryTokenEmbedded token);
        void Train(PredictorTrainingContext ctx);
        void LoadModel(PredictorPredictContext predictor);
        PredictDictionary Predict(PredictorPredictContext ctx, PredictDictionary input);
        List<PredictDictionary> PredictMultiple(PredictorPredictContext ctx, List<PredictDictionary> inputs);
        string[] GetAvailableDevices();
        List<PredictorCodification> GenerateCodifications(PredictorColumnEncodingSymbol encoding, ResultColumn resultColumn, PredictorColumnBase column);
        IEnumerable<PredictorColumnEncodingSymbol> GetRegisteredEncodingSymbols();
    }

    public interface IPredictorResultSaver
    {
        void AssertValid(PredictorEntity predictor);
        void SavePredictions(PredictorTrainingContext ctx);
    }

    public class PredictDictionary
    {
        public PredictDictionary(PredictorEntity predictor)
        {
            Predictor = predictor;
        }

        public Lite<Entity> Entity { get; set; } //Optional

        public PredictionOptions Options { get; set; }

        public PredictorEntity Predictor { get; set; }
        public Dictionary<PredictorColumnEmbedded, object> MainQueryValues { get; set; } = new Dictionary<PredictorColumnEmbedded, object>();
        public Dictionary<PredictorSubQueryEntity, PredictSubQueryDictionary> SubQueries { get; set; } = new Dictionary<PredictorSubQueryEntity, PredictSubQueryDictionary>();

        public PredictDictionary Clone()
        {
            var result = new PredictDictionary(Predictor)
            {
                Entity = this.Entity,
                Options = this.Options,
            };

            result.MainQueryValues.AddRange(MainQueryValues.ToDictionaryEx());
            result.SubQueries.AddRange(SubQueries, kvp => kvp.Key, kvp => kvp.Value.Clone());
            return result;
        }
    }

    public class PredictionOptions
    {
        public int? AlternativeCount;
        public List<PredictorCodification> FilteredCodifications;
    }

    public class AlternativePrediction
    {
        public float Probability { get; set; }
        public object Value { get; set; }
    }

    public class PredictSubQueryDictionary
    {
        public PredictSubQueryDictionary(PredictorSubQueryEntity subQuery)
        {
            SubQuery = subQuery;
        }

        public PredictorSubQueryEntity SubQuery { get; set; }
        public Dictionary<object[], Dictionary<PredictorSubQueryColumnEmbedded, object>> SubQueryGroups { get; set; } = new Dictionary<object[], Dictionary<PredictorSubQueryColumnEmbedded, object>>();

        public PredictSubQueryDictionary Clone()
        {
            var result = new PredictSubQueryDictionary(SubQuery);
            result.SubQueryGroups.AddRange(this.SubQueryGroups, kvp => kvp.Key, kvp => kvp.Value.ToDictionary());
            return result;
        }
    }
}
