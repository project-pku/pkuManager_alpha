﻿using pkuManager.Alerts;
using pkuManager.Formats.Fields;
using pkuManager.Formats.Modules.Templates;
using System.Numerics;
using static pkuManager.Alerts.Alert;
using static pkuManager.Formats.PorterDirective;

namespace pkuManager.Formats.Modules.Tags;

public interface Pokerus_O
{
    public IIntField Pokerus_Strain { get; }
    public IIntField Pokerus_Days { get; }
}

public interface Pokerus_E : Tag
{
    [PorterDirective(ProcessingPhase.FirstPass)]
    public void ExportPokerus()
    {
        Pokerus_O pokerusObj = Data as Pokerus_O;
        var defVals = new BigInteger[] { 0, 0 };
        var dataFields = new[] { pokerusObj.Pokerus_Strain, pokerusObj.Pokerus_Days };

        AlertType[] ats = NumericTagUtil.ExportMultiNumericTag(new[] { pku.Pokerus.Strain, pku.Pokerus.Days }, dataFields, defVals);
        Alert a = NumericTagUtil.GetMultiNumericAlert("Pokérus", new[] { "Pokérus strain", "Pokérus days" }, ats, defVals, dataFields, true);
        Warnings.Add(a);
    }
}