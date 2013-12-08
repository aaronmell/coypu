﻿using System;
using System.Linq;

namespace Coypu.Drivers
{
    /// <summary>
    /// Helper for formatting XPath queries
    /// </summary>
    public class XPath
    {
        private readonly bool uppercaseTagNames;

        public XPath(bool uppercaseTagNames = false)
        {
            this.uppercaseTagNames = uppercaseTagNames;
        }

        /// <summary>
        /// <para>Format an XPath query that uses string values for comparison that may contain single or double quotes</para>
        /// <para>Wraps the string in the appropriate quotes or uses concat() to separate them if both are present.</para>
        /// <para>Usage:</para>
        /// <code>  new XPath().Format(".//element[@attribute1 = {0} and @attribute2 = {1}]",inputOne,inputTwo) </code>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string Format(string value, params object[] args)
        {
            return String.Format(value, args.Select(a => Literal(a.ToString())).ToArray());
        }

        internal string Literal(string value)
        {
            if (HasNoDoubleQuotes(value))
                return WrapInDoubleQuotes(value);

            if (HasNoSingleQuotes(value))
                return WrapInSingleQuote(value);

            return BuildConcatSeparatingSingleAndDoubleQuotes(value);
        }

        private string BuildConcatSeparatingSingleAndDoubleQuotes(string value)
        {
            var doubleQuotedParts = value.Split('\"')
                                         .Select(WrapInDoubleQuotes)
                                         .ToArray();

            var reJoinedWithDoubleQuoteParts = String.Join(", '\"', ", doubleQuotedParts);

            return String.Format("concat({0})", TrimEmptyParts(reJoinedWithDoubleQuoteParts));
        }

        private string WrapInSingleQuote(string value)
        {
            return String.Format("'{0}'", value);
        }

        private string WrapInDoubleQuotes(string value)
        {
            return String.Format("\"{0}\"", value);
        }

        private string TrimEmptyParts(string concatArguments)
        {
            return concatArguments.Replace(", \"\"", "")
                                           .Replace("\"\", ", "");
        }

        private bool HasNoSingleQuotes(string value)
        {
            return !value.Contains("'");
        }

        private bool HasNoDoubleQuotes(string value)
        {
            return !value.Contains("\"");
        }

        public static string XPathNodeHasOneOfClasses(params string[] classNames)
        {
            return String.Join(" or ", classNames.Select(XPathNodeHasClass).ToArray());
        }

        public static string XPathNodeHasClass(string className)
        {
            return String.Format(" contains(@class,' {0}') " +
                                 "or contains(@class,'{0} ') " +
                                 "or contains(@class,' {0} ') ", className);
        }

        public string AttributeIsOneOf(string attributeName, string[] values)
        {
            return "(" + String.Join(" or ", values.Select(t => "@" + attributeName + "='" + t + "'").ToArray()) + ")";
        }

        public string TagNamedOneOf(params string[] fieldTagNames)
        {
            return "(" + string.Join(" or ", fieldTagNames.Select(t => "name()='" + CasedTagName(t) + "'").ToArray()) + ")";
        }

        public string CasedTagName(string tagName)
        {
            return uppercaseTagNames ? tagName.ToUpper() : tagName; ;
        }

        public string FrameXPath(string locator)
        {
            return Format(
                ".//*[" + TagNamedOneOf("iframe", "frame") + "]" +
                "[" + FrameAttributesMatch(locator) + "]",
                locator.Trim());
        }

        private string FrameAttributesMatch(string locator)
        {
            return AttributesMatchLocator(locator, true, "@id", "@name", "@title");
        }

        private static readonly string[] InputButtonTypes = new[] { "button", "submit", "image", "reset" };

        public string Button(string locator, Options options)
        {
            return Format(
                ".//*[" + IsInputButton() +
                "     or " + TagNamedOneOf("button") +
                "     or " + XPathNodeHasOneOfClasses("button", "btn") +
                "     or @role = 'button'" +
                "   ][" + AttributesMatchLocator(locator, true, "@id", "@name") + 
                "     or " + AttributesMatchLocator(locator.Trim(), options.Exact, "@value", "@alt", "normalize-space()") + 
                "]",
                locator.Trim());
        }

        private string AttributesMatchLocator(string locator, bool exact, params string[] attributes)
        {
            return string.Join(" or ", attributes.Select(a => Is(a, locator, exact)).ToArray());
        }

        private string IsInputButton()
        {
            return "(" + TagNamedOneOf("input") + " and " + AttributeIsOneOf("type", InputButtonTypes) + ")";
        }

        public string Fieldset(string locator, Options options)
        {
            return Format(".//fieldset[legend[" + IsText(locator, options.Exact) + "] or @id = {0}]", locator);
        }

        private static readonly string[] FieldTagNames = new[] { "input", "select", "textarea" };
        private static readonly string[] FieldInputTypes = new[] { "text", "password", "radio", "checkbox", "file", "email", "tel", "url" };
        private static readonly string[] FindByNameTypes = FieldInputTypes.Except(new[] { "radio" }).ToArray();
        private static readonly string[] FieldInputTypeWithHidden = FieldInputTypes.Union(new[] { "hidden" }).ToArray();
        private static readonly string[] FindByValueTypes = new[] { "checkbox", "radio" };

        public string Field(string locator, Options options)
        {
            return ForlabeledOrByAttribute(locator, options); // TODO: OR ContainerLabeled(locator, exact)
        }

        private string ForlabeledOrByAttribute(string locator, Options options)
        {
            return Format(
                ".//*[" + TagNamedOneOf(FieldTagNames) +
                "   and " +
                "   (" + IsLabelledWith(locator, options.Exact) +
                "      or " + HasIdOrPlaceholder(locator, options) +
                "      or " + HasName(locator) +
                "      or " + HasValue(locator) +
                "   )" +
                "]",
                locator);
        }

        private string ContainerLabeled(string locator, bool exact)
        {
            return Format(".//label[" + IsText(locator, exact) + "]//*[" + TagNamedOneOf(FieldTagNames) + "]", locator);
        }

        private string IsLabelledWith(string locator, bool exact)
        {
            return Format("(@id = //label[" + IsText(locator, exact) + "]/@for)", locator);
        }

        private string HasValue(string locator)
        {
            return Format("(" + AttributeIsOneOf("type", FindByValueTypes) + " and @value = {0})", locator);
        }

        private string HasName(string locator)
        {
            return Format("((" + AttributeIsOneOf("type", FindByNameTypes) + " or not(@type)) and @name = {0})", locator);
        }

        private string HasIdOrPlaceholder(string locator, Options options)
        {
            return Format("(" + IsAFieldInputType(options) + " and " + "(@id = {0} or " + Is("@placeholder", locator, options.Exact) + "))", locator);
        }

        public string Is(string selector, string locator, bool exact)
        {
            return exact 
                ? Format(selector + " = {0}", locator)
                : Format("contains(" + selector + ",{0})", locator);
        }

        public string IsText(string locator, bool exact)
        {
            return Is("normalize-space()", locator,exact);
        }

        private string IsAFieldInputType(Options options)
        {
            var fieldInputTypes = options.ConsiderInvisibleElements
                            ? FieldInputTypeWithHidden
                            : FieldInputTypes;

            return "(" + AttributeIsOneOf("type", fieldInputTypes) + " or not(@type))";
        }

        readonly string[] sectionTags = { "section", "div" };
        readonly string[] headerTags = { "h1", "h2", "h3", "h4", "h5", "h6" };

        public string Section(string locator, Options options)
        {
            return Format(".//*[" + TagNamedOneOf(sectionTags) +
                          " and (" +
                          "      ./*[" + TagNamedOneOf(headerTags) + " and " + IsText(locator, options.Exact) + " ]" +
                          "      or @id = {0}" +
                          "     )" +
                          "]", locator.Trim());
        }
    }
}