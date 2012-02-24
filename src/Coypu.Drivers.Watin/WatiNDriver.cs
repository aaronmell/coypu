﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

using SHDocVw;

using WatiN.Core;
using mshtml;

namespace Coypu.Drivers.Watin
{
    public class WatiNDriver : Driver
    {
        private readonly ElementFinder elementFinder;

        private DialogHandler watinDialogHandler;

        static WatiNDriver()
        {
            ElementFactory.RegisterElementType(typeof(Fieldset));
            ElementFactory.RegisterElementType(typeof(Section));
        }

        public WatiNDriver()
        {
            if (Configuration.Browser != Browser.InternetExplorer)
                throw new BrowserNotSupportedException(Configuration.Browser, GetType());

            Settings.AutoMoveMousePointerToTopLeft = false;

            Watin = CreateBrowser();
            elementFinder = new ElementFinder();
        }

        private WatiN.Core.Browser CreateBrowser()
        {
            var browser = new IEWithDialogWaiter();

            watinDialogHandler = new DialogHandler();
            browser.AddDialogHandler(watinDialogHandler);

            return browser;
        }

        internal WatiN.Core.Browser Watin { get; private set; }

        private static WatiN.Core.Element WatiNElement(Element element)
        {
            return WatiNElement<WatiN.Core.Element>(element);
        }

        private static T WatiNElement<T>(Element element)
            where T : WatiN.Core.Element
        {
            return element.Native as T;
        }

        private static Element BuildElement(WatiN.Core.Element element, string description)
        {
            if (element == null)
                throw new MissingHtmlException(description);
            return BuildElement(element);
        }

        private static Element BuildElement(WatiN.Core.Element element)
        {
            return new WatiNElement(element);
        }

        private static Element BuildElement(WatiN.Core.Browser browser)
        {
            return new WatiNBrowser(browser);
        }

        private static Element BuildElement(Frame frame, string description)
        {
            if (frame == null)
                throw new MissingHtmlException(description);
            return new WatiNFrame(frame);
        }

        public string ExecuteScript(string javascript)
        {
            var stripReturn = Regex.Replace(javascript, @"^\s*return ", "");
            var retval = Watin.Eval(stripReturn);
            Watin.WaitForComplete();
            return retval;
        }

        public Element FindFieldset(string locator, DriverScope scope)
        {
            return BuildElement(elementFinder.FindFieldset(locator, scope), "Failed to find fieldset: " + locator);
        }

        public Element FindSection(string locator, DriverScope scope)
        {
            return BuildElement(elementFinder.FindSection(locator, scope), "Failed to find section: " + locator);
        }

        public Element FindId(string id, DriverScope scope)
        {
            return BuildElement(elementFinder.FindElement(id, scope), "Failed to find id: " + id);
        }

        public Element FindIFrame(string locator, DriverScope scope)
        {
            return BuildElement(elementFinder.FindFrame(locator, scope), "Failed to find frame: " + locator);
        }

        public void Hover(Element element)
        {
            WatiNElement(element).FireEvent("onmouseover");
        }

        public IEnumerable<Cookie> GetBrowserCookies()
        {
            var ieBrowser = Watin as IE;
            if (ieBrowser == null)
                throw new NotSupportedException("Only supported for Internet Explorer");

            var persistentCookies = GetPersistentCookies(ieBrowser).ToList();
            var documentCookies = GetCookiesFromDocument(ieBrowser);

            var sessionCookies = documentCookies.Except(persistentCookies, new CookieNameEqualityComparer());

            return persistentCookies.Concat(sessionCookies).ToList();
        }

        private IEnumerable<Cookie> GetPersistentCookies(IE ieBrowser)
        {
            return ieBrowser.GetCookiesForUrl(Location).Cast<Cookie>();
        }

        private IEnumerable<Cookie> GetCookiesFromDocument(IE ieBrowser)
        {
            var document = ((IWebBrowser2)ieBrowser.InternetExplorer).Document as IHTMLDocument2;
            if (document == null)
                throw new InvalidOperationException("Cannot get IE document for cookies");

            return from untrimmedCookie in document.cookie.Split(';')
                   let cookie = untrimmedCookie.Trim()
                   let index = cookie.IndexOf('=')
                   let name = cookie.Substring(0, index)
                   let value = cookie.Substring(index + 1, cookie.Length - index - 1)
                   select new Cookie(name, value);
        }

        public Element FindButton(string locator, DriverScope scope)
        {
            return BuildElement(elementFinder.FindButton(locator, scope), "Failed to find button with text, id or name: " + locator);
        }

        public Element FindLink(string linkText, DriverScope scope)
        {
            return BuildElement(elementFinder.FindLink(linkText, scope), "Failed to find link with text: " + linkText);
        }

        public Element FindField(string locator, DriverScope scope)
        {
            return BuildElement(elementFinder.FindField(locator, scope), "Failed to find field with label, id, name or placeholder: " + locator);
        }

        public void Click(Element element)
        {
            // If we use Click, then we can get a deadlock if IE is displaying a modal dialog.
            // (Yay COM STA!) Our override of the IE class makes sure the WaitForComplete will
            // check to see if the main window is disabled before continuing with the normal wait
            var nativeElement = WatiNElement(element);
            nativeElement.ClickNoWait();
            nativeElement.WaitForComplete();
        }

        public void Visit(string url)
        {
            Watin.GoTo(url);
        }

        public void Set(Element element, string value)
        {
            var textField = WatiNElement<TextField>(element);
            if (textField != null)
            {
                textField.Value = value;
                return;
            }
            var fileUpload = WatiNElement<FileUpload>(element);
            if (fileUpload != null)
                fileUpload.Set(value);
        }

        public void Select(Element element, string option)
        {
            WatiNElement<SelectList>(element).SelectByTextOrValue(option);
        }

        public object Native
        {
            get { return Watin; }
        }

        public bool HasContent(string text, DriverScope scope)
        {
            var watiNScope = ElementFinder.WatiNScope(scope);
            if(watiNScope == Window.Native)
            {
                return ((WatiN.Core.Browser) Window.Native).Text.Contains(text);
            }
            return ((WatiN.Core.Element)watiNScope).Text.Contains(text);
        }

        public bool HasContentMatch(Regex pattern, DriverScope scope)
        {
            var watiNScope = ElementFinder.WatiNScope(scope);
            if (watiNScope == Window.Native)
            {
                return ((WatiN.Core.Browser)Window.Native).ContainsText(pattern);
            }
            return pattern.IsMatch(((WatiN.Core.Element)watiNScope).Text);
        }

        public void Check(Element field)
        {
            WatiNElement<CheckBox>(field).Checked = true;
        }

        public void Uncheck(Element field)
        {
            WatiNElement<CheckBox>(field).Checked = false;
        }

        public void Choose(Element field)
        {
            WatiNElement<RadioButton>(field).Checked = true;
        }

        public bool HasDialog(string withText)
        {
            return watinDialogHandler.Exists() && watinDialogHandler.Message == withText;
        }

        public Element Window
        {
            get { return BuildElement(Watin); }
        }

        public void AcceptModalDialog()
        {
            watinDialogHandler.ClickOk();
        }

        public void CancelModalDialog()
        {
            watinDialogHandler.ClickCancel();
        }

        public bool HasCss(string cssSelector, DriverScope scope)
        {
            return elementFinder.HasCss(cssSelector, scope);
        }

        public bool HasXPath(string xpath, DriverScope scope)
        {
            throw new NotSupportedException("HasXPath not yet implemented in WatiNDriver");
        }

        public Element FindCss(string cssSelector, DriverScope scope)
        {
            return BuildElement(elementFinder.FindCss(cssSelector, scope), "No element found by css: " + cssSelector);
        }

        public Element FindXPath(string xpath, DriverScope scope)
        {
            throw new NotSupportedException("FindXPath not yet implemented in WatiNDriver");
        }

        public IEnumerable<Element> FindAllCss(string cssSelector, DriverScope scope)
        {
            return (from e in elementFinder.FindAllCss(cssSelector, scope)
                    select BuildElement(e)).ToList();
        }

        public IEnumerable<Element> FindAllXPath(string xpath, DriverScope scope)
        {
            throw new NotSupportedException("FindAllXPath not yet implemented in WatiNDriver");
        }

        public Uri Location
        {
            get { return Watin.Uri; }
        }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            watinDialogHandler.Dispose();
            Watin.Dispose();
            Disposed = true;
        }
    }
}