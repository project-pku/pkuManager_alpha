﻿using pkuManager.WinForms.Alerts;
using pkuManager.WinForms.Formats.pku;
using pkuManager.WinForms.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static pkuManager.WinForms.Formats.PorterDirective;

namespace pkuManager.WinForms.Formats;

public abstract class Porter
{
    /// <summary>
    /// The name of the format this porter operates on.
    /// </summary>
    public abstract string FormatName { get; }

    /// <summary>
    /// The pku file being ported.
    /// </summary>
    public pkuObject pku { get; set; }

    /// <summary>
    /// The global flags to, optionally, be acted upon by the porter.
    /// </summary>
    public GlobalFlags GlobalFlags { get; }

    /// <summary>
    /// If this porter is Checking-In/Out a pku or just Importing/Exporting one.
    /// </summary>
    public bool CheckMode { get; protected set; }

    /// <summary>
    /// A data structure representing the non-pku format.
    /// </summary>
    public abstract FormatObject Data { get; }

    /// <summary>
    /// A list of notes to be displayed on the porter window.<br/>
    /// A note is an alert that, generally, regards some pre-processing directive.
    /// </summary>
    public List<Alert> Notes { get; } = new();

    /// <summary>
    /// A list of warnings to be displayed on the exporter window.<br/>
    /// A warning is an alert about a value that, generally, requires no input from the user.
    /// </summary>
    public List<Alert> Warnings { get; } = new();

    /// <summary>
    /// A list of errors to be displayed on the exporter window.<br/>
    /// An error is an alert about a value that, generally, requires input from the user.
    /// </summary>
    public List<Alert> Errors { get; } = new();

    /// <summary>
    /// Whether this porter is clear to use FirstHalf. True if <see cref="Reason"/> is null.
    /// </summary>
    public bool CanPort => Reason is null;

    /// <summary>
    /// Why this porter cannot be run, or null if it can. Should be set on construction.
    /// </summary>
    public string Reason { get; protected set; }

    /// <summary>
    /// A dictionary that maps a processing phase to all the members of the porter to be run in that phase.<br/>
    /// Initialized in the <see cref="FirstHalf"/> method.
    /// </summary>
    private Dictionary<ProcessingPhase, List<MemberInfo>> PorterDirectiveMap;

    /// <summary>
    /// Base porter constructor.
    /// </summary>
    /// <param name="pku">The pku to be ported.</param>
    /// <param name="globalFlags">The current collection's flag settings.</param>
    /// <param name="checkMode">Whether this porter is checking-in/out a pku.</param>
    public Porter(pkuObject pku, GlobalFlags globalFlags, bool checkMode)
    {
        this.pku = pku ?? throw new ArgumentException("Can't initialize an exporter with a null .pku!");
        GlobalFlags = globalFlags;
        CheckMode = checkMode;
    }

    /// <summary>
    /// Initializes the <see cref="PorterDirectiveMap"/> with all the members in this<br/>
    /// instance's class (and base classes/interfaces) with the <see cref="PorterDirective"/> attribute.
    /// </summary>
    private void InitPorterDirectiveMap()
    {
        List<MemberInfo> allMembers = new();
        void helper(Type type)
        {
            allMembers.AddRange(type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                      .Where(x => !allMembers.Any(y => y.Name == x.Name))); //no duplicate implementations (i.e. same name)
            foreach (Type baseInterface in type.GetInterfaces())
                helper(baseInterface);
        }
        helper(GetType());

        PorterDirectiveMap = new();
        foreach (ProcessingPhase p in Enum.GetValues(typeof(ProcessingPhase)) as ProcessingPhase[])
        {
            List<MemberInfo> members = new();
            members.AddRange(allMembers.Where(m =>
                (m.GetCustomAttributeWithInterface(typeof(PorterDirective)) as PorterDirective)?.Phase == p));
            PorterDirectiveMap[p] = members;
        }
    }

    /// <summary>
    /// Generates an exception to be thrown when an <see cref="PorterDirective"/> attribute is placed on an invalid member.
    /// </summary>
    /// <returns>An exception for members with invalid <see cref="PorterDirective"/> attributes.</returns>
    private static Exception InvalidAttributeException()
        => new($"{nameof(PorterDirective)} attributes can only be placed on" +
            $"a) void methods with no parameters" +
            $"b) properties of type {typeof(ErrorResolver<>).Name}, and" +
            $"c) properties of type {nameof(Action)}.");

    /// <summary>
    /// Given a list of <paramref name="members"/>, invokes void methods, <see cref="Action"/>s, and
    /// decides values for <see cref="ErrorResolver{T}"/>s respectively.
    /// </summary>
    /// <param name="members">A list of MemberInfo objects. Should not include any members but
    ///                       <see cref="ErrorResolver{T}"/>, and void parameterless methods.</param>
    private void RunMembers(List<MemberInfo> members)
    {
        void RunMember(MemberInfo member, List<MemberInfo> members)
        {
            if (member is null) //already consumed/dne
                return;

            PorterDirective ed = member.GetCustomAttributeWithInterface(typeof(PorterDirective)) as PorterDirective;

            // run prereqs first
            if (ed.Prerequisites?.Length > 0)
            {
                foreach (string prereq in ed.Prerequisites)
                    RunMember(members.FirstOrDefault(x => x.Name == prereq), members);
            }

            // Deal with void methods
            if (member.MemberType is MemberTypes.Method)
            {
                MethodInfo minfo = member as MethodInfo;
                if (minfo.ReturnType != typeof(void) && minfo.GetParameters() is not null)
                    throw InvalidAttributeException();
                minfo.Invoke(this, null);
            }
            //Deal with properties
            else if (member.MemberType is MemberTypes.Property)
            {
                var resolver = (member as PropertyInfo).GetValue(this);
                //Empty Resolvers
                if (resolver is null)
                    { } //do nothing
                //Actions
                else if (resolver is Action action)
                    action.Invoke();
                // ErrorResolvers
                else if (resolver.GetType().GetGenericTypeDefinition() == typeof(ErrorResolver<>))
                    resolver.GetType().GetMethod(nameof(ErrorResolver<object>.DecideValue)).Invoke(resolver, null);
                //Invalid properties
                else
                    throw InvalidAttributeException();
            }
            members.Remove(member); //consumed
        }

        while (members.Any())
            RunMember(members.First(), members);
    }

    /// <summary>
    /// Whether or not <see cref="FirstHalf"/> was run yet.
    /// </summary>
    private bool firstHalf = false;

    /// <summary>
    /// The first half of the exporting process. Runs all phases from
    /// <see cref="ProcessingPhase.PreProcessing"/>, to <see cref="ProcessingPhase.FirstPassStage2"/>.<br/>
    /// Should only be run if <see cref="CanPort"/> is true.
    /// </summary>
    public void FirstHalf()
    {
        if (!CanPort)
            throw new Exception("This .pku can't be exported to this format! This should not have happened...");

        InitPorterDirectiveMap(); //get all PorterDirectives

        RunMembers(PorterDirectiveMap[ProcessingPhase.PreProcessing]);
        RunMembers(PorterDirectiveMap[ProcessingPhase.FirstPass]);
        RunMembers(PorterDirectiveMap[ProcessingPhase.FirstPassStage2]);

        firstHalf = true;
    }

    /// <summary>
    /// The second half of the exporting process. Runs the <see cref="ProcessingPhase.SecondPass"/>
    /// and <see cref="ProcessingPhase.PostProcessing"/> phases.<br/>
    /// Should only be run after <see cref="FirstHalf"/>.
    /// </summary>
    protected void SecondHalf()
    {
        if (!firstHalf)
            throw new Exception($"{nameof(FirstHalf)} has not been run yet! This should not have happened...");

        RunMembers(PorterDirectiveMap[ProcessingPhase.SecondPass]);
        RunMembers(PorterDirectiveMap[ProcessingPhase.PostProcessing]);
    }
}

/// <summary>
/// An attribute to be placed on methods or <see cref="ErrorResolver{T}"/>s that
/// defines a <see cref=Porter"/>s order of operations.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
public class PorterDirective : Attribute
{
    /// <summary>
    /// The phase this member should be run in during the porting process.
    /// </summary>
    public ProcessingPhase Phase { get; }

    /// <summary>
    /// A list of members, in the same phase, that are to be run before this one.
    /// </summary>
    public string[] Prerequisites { get; }

    /// <summary>
    /// Creates an PorterDirective in the given <paramref name="phase"/> with the given <paramref name="prerequisites"/>.
    /// </summary>
    /// <param name="phase">The phase this member will run in.</param>
    /// <param name="prerequisites">A list of members, in the same phase, that are to be run before this one.</param>
    public PorterDirective(ProcessingPhase phase, params string[] prerequisites)
    {
        Phase = phase;
        Prerequisites = prerequisites;
    }

    /// <summary>
    /// A phase of the porting process.
    /// </summary>
    public enum ProcessingPhase
    {
        /// <summary>
        /// Usually for dealing with <see cref="GlobalFlags"/> before porting.
        /// </summary>
        PreProcessing,

        /// <summary>
        /// For generating the values and alerts that may need to be displayed to the user.
        /// </summary>
        FirstPass,

        /// <summary>
        /// For actions that need to be done after <see cref="FirstPass"/> but before <see cref="SecondPass"/>.
        /// </summary>
        FirstPassStage2,

        /// <summary>
        /// For deciding on which values to use in the final file, based on user input.
        /// </summary>
        SecondPass,

        /// <summary>
        /// For final edits/modifications to the exported file.
        /// </summary>
        PostProcessing
    };
}