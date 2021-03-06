﻿
import * as React from 'react'
import * as moment from 'moment'
import { Dic } from '@framework/Globals'
import { openModal } from '@framework/Modals'
import { TypeContext, StyleOptions, EntityFrame } from '@framework/TypeContext'
import { TypeInfo, getTypeInfo, parseId, GraphExplorer, PropertyRoute, ReadonlyBinding, } from '@framework/Reflection'
import * as Navigator from '@framework/Navigator'
import * as Operations from '@framework/Operations'
import { EntityPack, Entity, Lite, JavascriptMessage, NormalWindowMessage, entityInfo, getToString, is } from '@framework/Signum.Entities'
import { renderWidgets, renderEmbeddedWidgets, WidgetContext } from '@framework/Frames/Widgets'
import ValidationErrors from '@framework/Frames/ValidationErrors'
import ButtonBar from '@framework/Frames/ButtonBar'
import { CaseActivityEntity, WorkflowEntity, ICaseMainEntity, CaseActivityOperation, CaseActivityMessage } from '../Signum.Entities.Workflow'
import * as WorkflowClient from '../WorkflowClient'

interface CaseFromSenderInfoProps {
    current: CaseActivityEntity;
}

interface CaseFromSenderInfoState {
    prev?: CaseActivityEntity;
}

export default class CaseFromSenderInfo extends React.Component<CaseFromSenderInfoProps, CaseFromSenderInfoState>{

    constructor(props: CaseFromSenderInfoProps) {
        super(props);
        this.state = {};
    }

    componentWillMount() {
        this.load(this.props);
    }
    componentWillReceiveProps(newProps: CaseFromSenderInfoProps) {
        if (!is(this.props.current.previous, newProps.current.previous)) {
            this.load(newProps);
        }
    }

    load(props: CaseFromSenderInfoProps) {
        this.setState({ prev: undefined });
        if (props.current.previous)
            Navigator.API.fetchAndForget(props.current.previous!)
                .then(ca => this.setState({ prev: ca }))
                .done();
    }

    render() {

        const c = this.props.current;
        const p = this.state.prev;

        return (
            <div>
                {
                    c.previous == null ? null :
                        <div className="alert alert-info case-alert">
                            {p == null ? JavascriptMessage.loading.niceToString() :
                                CaseActivityMessage.From0On1.niceToString().formatHtml(
                                    <strong>{p.doneBy && p.doneBy.toStr}</strong>,
                                    p.doneDate && <strong>{moment(p.doneDate).format("L LT")} ({moment(p.doneDate).fromNow()})</strong>)
                            }
                        </div>
                }
                {
                    p && p.note && <div className="alert alert-warning case-alert">
                        <strong>{CaseActivityEntity.nicePropertyName(a => a.note)}:</strong>
                        {p.note.contains("\n")  ? "\n" : null}
                        {p.note}
                    </div>
                }
            </div>
        );
    }
}