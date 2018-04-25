//////////////////////////////////
//Auto-generated. Do NOT modify!//
//////////////////////////////////

import { MessageKey, QueryKey, Type, EnumType, registerSymbol } from '../../../Framework/Signum.React/Scripts/Reflection'
import * as Entities from '../../../Framework/Signum.React/Scripts/Signum.Entities'
import * as Basics from '../../../Framework/Signum.React/Scripts/Signum.Entities.Basics'
import * as Authorization from '../Authorization/Signum.Entities.Authorization'

interface IDynamicValidationEvaluator {}
interface IDynamicTypeConditionEvaluator {}

export const DynamicBaseType = new EnumType<DynamicBaseType>("DynamicBaseType");
export type DynamicBaseType =
    "Entity" |
    "MixinEntity" |
    "EmbeddedEntity";

export const DynamicExpressionEntity = new Type<DynamicExpressionEntity>("DynamicExpression");
export interface DynamicExpressionEntity extends Entities.Entity {
    Type: "DynamicExpression";
    name?: string | null;
    fromType?: string | null;
    returnType?: string | null;
    body?: string | null;
    format?: string | null;
    unit?: string | null;
    translation?: DynamicExpressionTranslation;
}

export module DynamicExpressionOperation {
    export const Clone : Entities.ConstructSymbol_From<DynamicExpressionEntity, DynamicExpressionEntity> = registerSymbol("Operation", "DynamicExpressionOperation.Clone");
    export const Save : Entities.ExecuteSymbol<DynamicExpressionEntity> = registerSymbol("Operation", "DynamicExpressionOperation.Save");
    export const Delete : Entities.DeleteSymbol<DynamicExpressionEntity> = registerSymbol("Operation", "DynamicExpressionOperation.Delete");
}

export const DynamicExpressionTranslation = new EnumType<DynamicExpressionTranslation>("DynamicExpressionTranslation");
export type DynamicExpressionTranslation =
    "TranslateExpressionName" |
    "ReuseTranslationOfReturnType" |
    "NoTranslation";

export const DynamicMixinConnectionEntity = new Type<DynamicMixinConnectionEntity>("DynamicMixinConnection");
export interface DynamicMixinConnectionEntity extends Entities.Entity {
    Type: "DynamicMixinConnection";
    entityType?: Entities.Lite<Basics.TypeEntity> | null;
    dynamicMixin?: Entities.Lite<DynamicTypeEntity> | null;
}

export module DynamicMixinConnectionOperation {
    export const Save : Entities.ExecuteSymbol<DynamicMixinConnectionEntity> = registerSymbol("Operation", "DynamicMixinConnectionOperation.Save");
    export const Delete : Entities.DeleteSymbol<DynamicMixinConnectionEntity> = registerSymbol("Operation", "DynamicMixinConnectionOperation.Delete");
}

export module DynamicPanelPermission {
    export const ViewDynamicPanel : Authorization.PermissionSymbol = registerSymbol("Permission", "DynamicPanelPermission.ViewDynamicPanel");
    export const RestartApplication : Authorization.PermissionSymbol = registerSymbol("Permission", "DynamicPanelPermission.RestartApplication");
}

export const DynamicRenameEntity = new Type<DynamicRenameEntity>("DynamicRename");
export interface DynamicRenameEntity extends Entities.Entity {
    Type: "DynamicRename";
    creationDate?: string;
    replacementKey?: string | null;
    oldName?: string | null;
    newName?: string | null;
}

export const DynamicSqlMigrationEntity = new Type<DynamicSqlMigrationEntity>("DynamicSqlMigration");
export interface DynamicSqlMigrationEntity extends Entities.Entity {
    Type: "DynamicSqlMigration";
    creationDate?: string;
    createdBy?: Entities.Lite<Basics.IUserEntity> | null;
    executionDate?: string | null;
    executedBy?: Entities.Lite<Basics.IUserEntity> | null;
    comment?: string | null;
    script?: string | null;
}

export module DynamicSqlMigrationMessage {
    export const TheMigrationIsAlreadyExecuted = new MessageKey("DynamicSqlMigrationMessage", "TheMigrationIsAlreadyExecuted");
    export const PreventingGenerationNewScriptBecauseOfErrorsInDynamicCodeFixErrorsAndRestartServer = new MessageKey("DynamicSqlMigrationMessage", "PreventingGenerationNewScriptBecauseOfErrorsInDynamicCodeFixErrorsAndRestartServer");
}

export module DynamicSqlMigrationOperation {
    export const Create : Entities.ConstructSymbol_Simple<DynamicSqlMigrationEntity> = registerSymbol("Operation", "DynamicSqlMigrationOperation.Create");
    export const Save : Entities.ExecuteSymbol<DynamicSqlMigrationEntity> = registerSymbol("Operation", "DynamicSqlMigrationOperation.Save");
    export const Execute : Entities.ExecuteSymbol<DynamicSqlMigrationEntity> = registerSymbol("Operation", "DynamicSqlMigrationOperation.Execute");
    export const Delete : Entities.DeleteSymbol<DynamicSqlMigrationEntity> = registerSymbol("Operation", "DynamicSqlMigrationOperation.Delete");
}

export const DynamicTypeConditionEntity = new Type<DynamicTypeConditionEntity>("DynamicTypeCondition");
export interface DynamicTypeConditionEntity extends Entities.Entity {
    Type: "DynamicTypeCondition";
    symbolName?: DynamicTypeConditionSymbolEntity | null;
    entityType?: Basics.TypeEntity | null;
    eval: DynamicTypeConditionEval;
}

export const DynamicTypeConditionEval = new Type<DynamicTypeConditionEval>("DynamicTypeConditionEval");
export interface DynamicTypeConditionEval extends EvalEmbedded<IDynamicTypeConditionEvaluator> {
    Type: "DynamicTypeConditionEval";
}

export module DynamicTypeConditionOperation {
    export const Clone : Entities.ConstructSymbol_From<DynamicTypeConditionEntity, DynamicTypeConditionEntity> = registerSymbol("Operation", "DynamicTypeConditionOperation.Clone");
    export const Save : Entities.ExecuteSymbol<DynamicTypeConditionEntity> = registerSymbol("Operation", "DynamicTypeConditionOperation.Save");
}

export const DynamicTypeConditionSymbolEntity = new Type<DynamicTypeConditionSymbolEntity>("DynamicTypeConditionSymbol");
export interface DynamicTypeConditionSymbolEntity extends Entities.Entity {
    Type: "DynamicTypeConditionSymbol";
    name?: string | null;
}

export module DynamicTypeConditionSymbolOperation {
    export const Save : Entities.ExecuteSymbol<DynamicTypeConditionSymbolEntity> = registerSymbol("Operation", "DynamicTypeConditionSymbolOperation.Save");
}

export const DynamicTypeEntity = new Type<DynamicTypeEntity>("DynamicType");
export interface DynamicTypeEntity extends Entities.Entity {
    Type: "DynamicType";
    baseType?: DynamicBaseType;
    typeName?: string | null;
    typeDefinition?: string | null;
}

export module DynamicTypeMessage {
    export const DynamicType0SucessfullySavedGoToDynamicPanelNow = new MessageKey("DynamicTypeMessage", "DynamicType0SucessfullySavedGoToDynamicPanelNow");
    export const ServerRestartedWithErrorsInDynamicCodeFixErrorsAndRestartAgain = new MessageKey("DynamicTypeMessage", "ServerRestartedWithErrorsInDynamicCodeFixErrorsAndRestartAgain");
    export const RemoveSaveOperation = new MessageKey("DynamicTypeMessage", "RemoveSaveOperation");
    export const TheEntityShouldBeSynchronizedToApplyMixins = new MessageKey("DynamicTypeMessage", "TheEntityShouldBeSynchronizedToApplyMixins");
}

export module DynamicTypeOperation {
    export const Create : Entities.ConstructSymbol_Simple<DynamicTypeEntity> = registerSymbol("Operation", "DynamicTypeOperation.Create");
    export const Clone : Entities.ConstructSymbol_From<DynamicTypeEntity, DynamicTypeEntity> = registerSymbol("Operation", "DynamicTypeOperation.Clone");
    export const Save : Entities.ExecuteSymbol<DynamicTypeEntity> = registerSymbol("Operation", "DynamicTypeOperation.Save");
    export const Delete : Entities.DeleteSymbol<DynamicTypeEntity> = registerSymbol("Operation", "DynamicTypeOperation.Delete");
}

export const DynamicValidationEntity = new Type<DynamicValidationEntity>("DynamicValidation");
export interface DynamicValidationEntity extends Entities.Entity {
    Type: "DynamicValidation";
    name?: string | null;
    entityType?: Basics.TypeEntity | null;
    propertyRoute?: Basics.PropertyRouteEntity | null;
    eval: DynamicValidationEval;
}

export const DynamicValidationEval = new Type<DynamicValidationEval>("DynamicValidationEval");
export interface DynamicValidationEval extends EvalEmbedded<IDynamicValidationEvaluator> {
    Type: "DynamicValidationEval";
}

export module DynamicValidationOperation {
    export const Save : Entities.ExecuteSymbol<DynamicValidationEntity> = registerSymbol("Operation", "DynamicValidationOperation.Save");
}

export const DynamicViewEntity = new Type<DynamicViewEntity>("DynamicView");
export interface DynamicViewEntity extends Entities.Entity {
    Type: "DynamicView";
    viewName?: string | null;
    entityType?: Basics.TypeEntity | null;
    viewContent?: string | null;
}

export module DynamicViewMessage {
    export const AddChild = new MessageKey("DynamicViewMessage", "AddChild");
    export const AddSibling = new MessageKey("DynamicViewMessage", "AddSibling");
    export const Remove = new MessageKey("DynamicViewMessage", "Remove");
    export const GenerateChildren = new MessageKey("DynamicViewMessage", "GenerateChildren");
    export const ClearChildren = new MessageKey("DynamicViewMessage", "ClearChildren");
    export const SelectATypeOfComponent = new MessageKey("DynamicViewMessage", "SelectATypeOfComponent");
    export const SelectANodeFirst = new MessageKey("DynamicViewMessage", "SelectANodeFirst");
    export const UseExpression = new MessageKey("DynamicViewMessage", "UseExpression");
    export const SuggestedFindOptions = new MessageKey("DynamicViewMessage", "SuggestedFindOptions");
    export const TheFollowingQueriesReference0 = new MessageKey("DynamicViewMessage", "TheFollowingQueriesReference0");
    export const ChooseAView = new MessageKey("DynamicViewMessage", "ChooseAView");
    export const SinceThereIsNoDynamicViewSelectorYouNeedToChooseAViewManually = new MessageKey("DynamicViewMessage", "SinceThereIsNoDynamicViewSelectorYouNeedToChooseAViewManually");
    export const ExampleEntity = new MessageKey("DynamicViewMessage", "ExampleEntity");
    export const ShowHelp = new MessageKey("DynamicViewMessage", "ShowHelp");
    export const HideHelp = new MessageKey("DynamicViewMessage", "HideHelp");
}

export module DynamicViewOperation {
    export const Create : Entities.ConstructSymbol_Simple<DynamicViewEntity> = registerSymbol("Operation", "DynamicViewOperation.Create");
    export const Clone : Entities.ConstructSymbol_From<DynamicViewEntity, DynamicViewEntity> = registerSymbol("Operation", "DynamicViewOperation.Clone");
    export const Save : Entities.ExecuteSymbol<DynamicViewEntity> = registerSymbol("Operation", "DynamicViewOperation.Save");
    export const Delete : Entities.DeleteSymbol<DynamicViewEntity> = registerSymbol("Operation", "DynamicViewOperation.Delete");
}

export const DynamicViewOverrideEntity = new Type<DynamicViewOverrideEntity>("DynamicViewOverride");
export interface DynamicViewOverrideEntity extends Entities.Entity {
    Type: "DynamicViewOverride";
    entityType?: Basics.TypeEntity | null;
    viewName?: string | null;
    script?: string | null;
}

export module DynamicViewOverrideOperation {
    export const Save : Entities.ExecuteSymbol<DynamicViewOverrideEntity> = registerSymbol("Operation", "DynamicViewOverrideOperation.Save");
    export const Delete : Entities.DeleteSymbol<DynamicViewOverrideEntity> = registerSymbol("Operation", "DynamicViewOverrideOperation.Delete");
}

export const DynamicViewSelectorEntity = new Type<DynamicViewSelectorEntity>("DynamicViewSelector");
export interface DynamicViewSelectorEntity extends Entities.Entity {
    Type: "DynamicViewSelector";
    entityType?: Basics.TypeEntity | null;
    script?: string | null;
}

export module DynamicViewSelectorOperation {
    export const Save : Entities.ExecuteSymbol<DynamicViewSelectorEntity> = registerSymbol("Operation", "DynamicViewSelectorOperation.Save");
    export const Delete : Entities.DeleteSymbol<DynamicViewSelectorEntity> = registerSymbol("Operation", "DynamicViewSelectorOperation.Delete");
}

export module DynamicViewValidationMessage {
    export const OnlyChildNodesOfType0Allowed = new MessageKey("DynamicViewValidationMessage", "OnlyChildNodesOfType0Allowed");
    export const Type0DoesNotContainsField1 = new MessageKey("DynamicViewValidationMessage", "Type0DoesNotContainsField1");
    export const Member0IsMandatoryFor1 = new MessageKey("DynamicViewValidationMessage", "Member0IsMandatoryFor1");
    export const _0RequiresA1 = new MessageKey("DynamicViewValidationMessage", "_0RequiresA1");
    export const Entity = new MessageKey("DynamicViewValidationMessage", "Entity");
    export const CollectionOfEntities = new MessageKey("DynamicViewValidationMessage", "CollectionOfEntities");
    export const Value = new MessageKey("DynamicViewValidationMessage", "Value");
    export const CollectionOfEnums = new MessageKey("DynamicViewValidationMessage", "CollectionOfEnums");
    export const EntityOrValue = new MessageKey("DynamicViewValidationMessage", "EntityOrValue");
    export const FilteringWithNew0ConsiderChangingVisibility = new MessageKey("DynamicViewValidationMessage", "FilteringWithNew0ConsiderChangingVisibility");
    export const AggregateIsMandatoryFor01 = new MessageKey("DynamicViewValidationMessage", "AggregateIsMandatoryFor01");
    export const ValueTokenCanNotBeUseFor0BecauseIsNotAnEntity = new MessageKey("DynamicViewValidationMessage", "ValueTokenCanNotBeUseFor0BecauseIsNotAnEntity");
    export const ViewNameIsNotAllowedWhileHavingChildren = new MessageKey("DynamicViewValidationMessage", "ViewNameIsNotAllowedWhileHavingChildren");
}

export interface EvalEmbedded<T> extends Entities.EmbeddedEntity {
    script?: string | null;
}


