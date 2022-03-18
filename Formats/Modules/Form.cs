﻿using OneOf;
using pkuManager.Alerts;
using pkuManager.Formats.Fields;
using pkuManager.Formats.pku;
using pkuManager.Utilities;
using System.Collections.Generic;
using System.Numerics;
using static pkuManager.Formats.PorterDirective;

namespace pkuManager.Formats.Modules;

public interface Form_O
{
    public OneOf<IField<BigInteger>, IField<string>> Form { get; }
}

public interface Form_E
{
    public pkuObject pku { get; }
    public List<Alert> Warnings { get; }
    public string FormatName { get; }

    public Form_O Form_Field { get; }

    public void ProcessFormBase() => Form_Field.Form.Switch(
        x => x.SetAs(DexUtil.GetSpeciesIndexedValue<int?>(pku, FormatName, "Form Indices").Value), //int index
        x => x.Value = DexUtil.GetSpeciesIndexedValue<string>(pku, FormatName, "Form Indices") //string index
    );

    [PorterDirective(ProcessingPhase.FirstPass)]
    protected void ProcessForm() => ProcessFormBase();
}