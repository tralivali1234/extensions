//////////////////////////////////
//Auto-generated. Do NOT modify!//
//////////////////////////////////

import { MessageKey, QueryKey, Type, EnumType, registerSymbol } from '../../../Framework/Signum.React/Scripts/Reflection'
import * as Entities from '../../../Framework/Signum.React/Scripts/Signum.Entities'
import * as Basics from '../../../Framework/Signum.React/Scripts/Signum.Entities.Basics'
import * as UserQueries from '../UserQueries/Signum.Entities.UserQueries'
import * as UserAssets from '../UserAssets/Signum.Entities.UserAssets'
import * as Files from '../Files/Signum.Entities.Files'


export const ActivationFunction = new EnumType<ActivationFunction>("ActivationFunction");
export type ActivationFunction =
    "ReLU" |
    "Tanh" |
    "Sigmoid" |
    "Linear";

export interface IPredictorAlgorithmSettings extends Entities.Entity {
}

export const NeuronalNetworkSettingsEntity = new Type<NeuronalNetworkSettingsEntity>("NeuronalNetworkSettings");
export interface NeuronalNetworkSettingsEntity extends Entities.Entity, IPredictorAlgorithmSettings {
    Type: "NeuronalNetworkSettings";
    learningRate?: number;
    activationFunction?: ActivationFunction;
    regularization?: Regularization;
    regularizationRate?: number;
    trainingRatio?: number;
    backSize?: number;
    neuronalNetworkDescription?: string | null;
}

export const PredictorAlgorithmSymbol = new Type<PredictorAlgorithmSymbol>("PredictorAlgorithm");
export interface PredictorAlgorithmSymbol extends Entities.Symbol {
    Type: "PredictorAlgorithm";
}

export const PredictorCodificationEntity = new Type<PredictorCodificationEntity>("PredictorCodification");
export interface PredictorCodificationEntity extends Entities.Entity {
    Type: "PredictorCodification";
    predictor?: Entities.Lite<PredictorEntity> | null;
    columnIndex?: number;
    originalColumnIndex?: number;
    groupKey0?: string | null;
    groupKey1?: string | null;
    groupKey2?: string | null;
    isValue?: string | null;
}

export const PredictorColumnEmbedded = new Type<PredictorColumnEmbedded>("PredictorColumnEmbedded");
export interface PredictorColumnEmbedded extends Entities.EmbeddedEntity {
    Type: "PredictorColumnEmbedded";
    type?: PredictorColumnType;
    usage?: PredictorColumnUsage;
    token?: UserAssets.QueryTokenEmbedded | null;
    multiColumn?: PredictorMultiColumnEntity | null;
}

export const PredictorColumnType = new EnumType<PredictorColumnType>("PredictorColumnType");
export type PredictorColumnType =
    "SimpleColumn" |
    "MultiColumn";

export const PredictorColumnUsage = new EnumType<PredictorColumnUsage>("PredictorColumnUsage");
export type PredictorColumnUsage =
    "Input" |
    "Output";

export const PredictorEntity = new Type<PredictorEntity>("Predictor");
export interface PredictorEntity extends Entities.Entity {
    Type: "Predictor";
    query?: Basics.QueryEntity | null;
    name?: string | null;
    settings?: PredictorSettingsEmbedded | null;
    algorithm?: PredictorAlgorithmSymbol | null;
    algorithmSettings?: IPredictorAlgorithmSettings | null;
    state?: PredictorState;
    filters: Entities.MList<UserQueries.QueryFilterEmbedded>;
    columns: Entities.MList<PredictorColumnEmbedded>;
    files: Entities.MList<PredictorFileEmbedded>;
}

export const PredictorFileEmbedded = new Type<PredictorFileEmbedded>("PredictorFileEmbedded");
export interface PredictorFileEmbedded extends Entities.EmbeddedEntity {
    Type: "PredictorFileEmbedded";
    key?: string | null;
    file?: Files.FilePathEmbedded | null;
}

export module PredictorMessage {
    export const Csv = new MessageKey("PredictorMessage", "Csv");
    export const Tsv = new MessageKey("PredictorMessage", "Tsv");
    export const TsvMetadata = new MessageKey("PredictorMessage", "TsvMetadata");
    export const TensorflowProjector = new MessageKey("PredictorMessage", "TensorflowProjector");
    export const DownloadCsv = new MessageKey("PredictorMessage", "DownloadCsv");
    export const DownloadTsv = new MessageKey("PredictorMessage", "DownloadTsv");
    export const DownloadTsvMetadata = new MessageKey("PredictorMessage", "DownloadTsvMetadata");
    export const OpenTensorflowProjector = new MessageKey("PredictorMessage", "OpenTensorflowProjector");
}

export const PredictorMultiColumnEntity = new Type<PredictorMultiColumnEntity>("PredictorMultiColumn");
export interface PredictorMultiColumnEntity extends Entities.Entity {
    Type: "PredictorMultiColumn";
    query?: Basics.QueryEntity | null;
    additionalFilters: Entities.MList<UserQueries.QueryFilterEmbedded>;
    groupKeys: Entities.MList<UserAssets.QueryTokenEmbedded>;
    aggregates: Entities.MList<UserAssets.QueryTokenEmbedded>;
}

export module PredictorOperation {
    export const Save : Entities.ExecuteSymbol<PredictorEntity> = registerSymbol("Operation", "PredictorOperation.Save");
    export const Train : Entities.ExecuteSymbol<PredictorEntity> = registerSymbol("Operation", "PredictorOperation.Train");
    export const Untrain : Entities.ExecuteSymbol<PredictorEntity> = registerSymbol("Operation", "PredictorOperation.Untrain");
    export const Delete : Entities.DeleteSymbol<PredictorEntity> = registerSymbol("Operation", "PredictorOperation.Delete");
    export const Clone : Entities.ConstructSymbol_From<PredictorEntity, PredictorEntity> = registerSymbol("Operation", "PredictorOperation.Clone");
}

export const PredictorSettingsEmbedded = new Type<PredictorSettingsEmbedded>("PredictorSettingsEmbedded");
export interface PredictorSettingsEmbedded extends Entities.EmbeddedEntity {
    Type: "PredictorSettingsEmbedded";
}

export const PredictorState = new EnumType<PredictorState>("PredictorState");
export type PredictorState =
    "Draft" |
    "Trained";

export const Regularization = new EnumType<Regularization>("Regularization");
export type Regularization =
    "None" |
    "L1" |
    "L2";

