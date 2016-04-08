﻿using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using Windows.ApplicationModel.Resources;

namespace LocationAlarm.WindowsPhoneLib
{
    // http://blogs.msdn.com/b/philliphoff/archive/2014/11/19/missingmanifestresourceexception-when-using-portable-class-libraries-in-winrt.aspx
    public class WindowsRuntimeResourceManager : ResourceManager
    {
        private readonly ResourceLoader _resourceLoader;

        private WindowsRuntimeResourceManager(string baseName, Assembly assembly) : base(baseName, assembly)
        {
            _resourceLoader = ResourceLoader.GetForViewIndependentUse(baseName);
        }

        public static void InjectIntoResxGeneratedApplicationResourcesClass(Type resxGeneratedApplicationResourcesClass)
        {
            resxGeneratedApplicationResourcesClass.GetRuntimeFields()
                .First(m => m.Name == "resourceMan")
                .SetValue(null, new WindowsRuntimeResourceManager(resxGeneratedApplicationResourcesClass.FullName,
                    resxGeneratedApplicationResourcesClass.GetTypeInfo().Assembly));
        }

        public override string GetString(string name, CultureInfo culture)
        {
            return _resourceLoader.GetString(name);
        }
    }
}