﻿import * as React from 'react'
import { Link } from 'react-router'
import * as numbro from 'numbro'
import * as moment from 'moment'
import { Dic } from '../../../../Framework/Signum.React/Scripts/Globals'
import * as Finder from '../../../../Framework/Signum.React/Scripts/Finder'
import { notifySuccess } from '../../../../Framework/Signum.React/Scripts/Operations/EntityOperations'
import { CountSearchControl, SearchControl } from '../../../../Framework/Signum.React/Scripts/Search'
import EntityLink from '../../../../Framework/Signum.React/Scripts/SearchControl/EntityLink'
import { QueryDescription, SubTokensOptions } from '../../../../Framework/Signum.React/Scripts/FindOptions'
import { getQueryNiceName, PropertyRoute, getTypeInfos } from '../../../../Framework/Signum.React/Scripts/Reflection'
import { ModifiableEntity, EntityControlMessage, Entity, Lite, parseLite, getToString, JavascriptMessage } from '../../../../Framework/Signum.React/Scripts/Signum.Entities'
import * as CultureClient from '../CultureClient'
import { API, AssemblyResult, LocalizedType, LocalizableType } from '../TranslationClient'
import { CultureInfoEntity } from '../../Basics/Signum.Entities.Basics'
import { TranslationMessage } from '../Signum.Entities.Translation'
import { TranslationTypeTable } from './TranslationCodeView'

require("../Translation.css");

interface TranslationCodeSyncProps extends ReactRouter.RouteComponentProps<{}, { culture: string; assembly: string }> {

}

export default class TranslationCodeSync extends React.Component<TranslationCodeSyncProps, { result?: AssemblyResult; cultures?: { [name: string]: Lite<CultureInfoEntity> } }> {

    constructor(props) {
        super(props);
        this.state = {};
    }

    componentWillMount() {
        CultureClient.getCultures().then(cultures => this.setState({ cultures })).done();

        this.loadSync().done();
    }

    loadSync() {
        var { assembly, culture } = this.props.routeParams;
        return API.sync(assembly, culture).then(result => this.setState({ result }))
    }

    render() {

        var {assembly, culture } = this.props.routeParams;

        var message = TranslationMessage.Synchronize0In1.niceToString(assembly,
                this.state.cultures ? this.state.cultures[culture].toStr : culture);

        return (
            <div>
                <h2>{message}</h2>
                <br/>
                { this.renderTable() }
            </div>
        );
    }

    handleSearch = (filter: string) => {
        var {assembly, culture} = this.props.routeParams;

        return API.retrieve(assembly, culture || "", filter)
            .then(result => this.setState({ result: result }))
            .done();
    }

    renderTable() {

        if (this.state.result == null)
            return null;


        if (Dic.getKeys(this.state.result).length == 0)
            return <strong> {TranslationMessage.NoResultsFound.niceToString() }</strong>;

        return (
            <div>
                { Dic.getValues(this.state.result.types).map(type => <TranslationTypeTable key={type.type} type={type} result={this.state.result} currentCulture={this.props.routeParams.culture} />) }
                <input type="submit" value={ TranslationMessage.Save.niceToString() } className="btn btn-primary" onClick={this.handleSave}/>
            </div>
        );
    }

    handleSave = (e: React.FormEvent) => {
        e.preventDefault();
        var params = this.props.routeParams;
        API.save(params.assembly, params.culture || "", this.state.result)
            .then(() => notifySuccess())
            .then(() => this.loadSync())
            .done();
    }
}
