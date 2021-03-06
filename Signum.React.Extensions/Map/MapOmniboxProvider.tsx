﻿import * as React from 'react'
import { Lite, Entity } from '@framework/Signum.Entities'
import { getQueryKey, getQueryNiceName } from '@framework/Reflection'
import { OmniboxMessage } from '../Omnibox/Signum.Entities.Omnibox'
import { OmniboxResult, OmniboxMatch, OmniboxProvider } from '../Omnibox/OmniboxClient'
import { QueryToken, FilterOperation, FindOptions, FilterOption } from '@framework/FindOptions'
import * as Navigator from '@framework/Navigator'
import * as Finder from '@framework/Finder'
import { MapMessage } from './Signum.Entities.Map'



export default class MapOmniboxProvider extends OmniboxProvider<MapOmniboxResult>
{
    getProviderName() {
        return "MapOmniboxResult";
    }

    icon() {
        return this.coloredIcon("map", "green");
    }

    renderItem(result: MapOmniboxResult): React.ReactChild[] {

        const array: React.ReactChild[] = [];

        array.push(this.icon());

        this.renderMatch(result.KeywordMatch, array);
        array.push("\u0020");

        if (result.TypeMatch != undefined)
            this.renderMatch(result.TypeMatch, array);
        
        return array;
    }

    navigateTo(result: MapOmniboxResult) {

        if (result.KeywordMatch == undefined)
            return undefined;

        return Promise.resolve("~/Map" + (result.TypeName ? "/" + result.TypeName : ""));
    }

    toString(result: MapOmniboxResult) {
        if (result.TypeMatch == undefined)
            return result.KeywordMatch.Text;

        return "{0} {1}".formatWith(result.KeywordMatch.Text, result.TypeMatch.Text);
    }
}

interface MapOmniboxResult extends OmniboxResult {
    KeywordMatch: OmniboxMatch;

    TypeName: string;
    TypeMatch: OmniboxMatch;
}
