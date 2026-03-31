// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;

namespace realvirtual.MCP
{
    //! Marks a parameter of an MCP tool method with a description.
    //!
    //! This description is included in the JSON schema sent to AI agents
    //! to help them understand what value to provide for the parameter.
    //!
    //! Example:
    //! public static string DriveTo([McpParam("Name of the drive")] string name)
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class McpParamAttribute : Attribute
    {
        //! Description of the parameter's purpose and expected value
        public string Description { get; }

        //! Creates an MCP parameter attribute with description.
        //! @param description Brief description of the parameter
        public McpParamAttribute(string description)
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }
    }
}
