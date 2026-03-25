// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;

namespace realvirtual.MCP
{
    //! Marks a static method as an MCP tool that can be called by AI agents.
    //!
    //! The method must be public static and return a string (preferably JSON).
    //! Method name will be converted from PascalCase to snake_case automatically.
    //!
    //! Example:
    //! [McpTool("Start the simulation")]
    //! public static string SimPlay() { return "{\"status\":\"playing\"}"; }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class McpToolAttribute : Attribute
    {
        //! Description of what the tool does (shown to AI agents)
        public string Description { get; }

        //! Optional override for the tool name (defaults to method name converted to snake_case)
        public string Name { get; }

        //! Creates an MCP tool attribute with description and optional name override.
        //! @param description Brief description of the tool's purpose and function
        //! @param name Optional tool name override (if null, uses method name converted to snake_case)
        public McpToolAttribute(string description, string name = null)
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Name = name;
        }
    }
}
