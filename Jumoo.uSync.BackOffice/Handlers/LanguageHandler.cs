﻿namespace Jumoo.uSync.BackOffice.Handlers
{
    using System;
    using System.IO;
    using System.Xml.Linq;

    using Umbraco.Core;
    using Umbraco.Core.Models;
    using Umbraco.Core.Services;
    using Umbraco.Core.Logging;

    using Jumoo.uSync.Core;
    using Jumoo.uSync.BackOffice.Helpers;
    using System.Collections.Generic;

    public class LanguageHandler : uSyncBaseHandler<ILanguage>, ISyncHandler
    {
        public string Name { get { return "uSync: LanguageHandler"; } }
        public int Priority { get { return uSyncConstants.Priority.Languages; } }
        public string SyncFolder { get { return Constants.Packaging.LanguagesNodeName; } }

        public override SyncAttempt<ILanguage> Import(string filePath, bool force = false)
        {
            if (!System.IO.File.Exists(filePath))
                throw new FileNotFoundException(filePath);

            var node = XElement.Load(filePath);
            return uSyncCoreContext.Instance.LanguageSerializer.DeSerialize(node, force);
        }

        public IEnumerable<uSyncAction> ExportAll(string folder)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            var _languageService = ApplicationContext.Current.Services.LocalizationService;
            foreach (var item in _languageService.GetAllLanguages())
            {
                if (item != null)
                    actions.Add(ExportToDisk(item, folder));
            }
            return actions;
        }

        public uSyncAction ExportToDisk(ILanguage item, string folder)
        {
            if (item == null)
                return uSyncAction.Fail(Path.GetFileName(folder), typeof(ILanguage), "item not set");

            try
            {
                var attempt = uSyncCoreContext.Instance.LanguageSerializer.Serialize(item);
                var filename = string.Empty;

                if (attempt.Success)
                {
                    filename = uSyncIOHelper.SavePath(folder, SyncFolder, item.CultureName.ToSafeAlias());
                    uSyncIOHelper.SaveNode(attempt.Item,filename);
                }
                return uSyncActionHelper<XElement>.SetAction(attempt, filename);

            }
            catch (Exception ex)
            {
                return uSyncAction.Fail(item.CultureName, item.GetType(), ChangeType.Export, ex);

            }
        }

        public void RegisterEvents()
        {
            LocalizationService.SavedLanguage += LocalizationService_SavedLanguage;
            LocalizationService.DeletedLanguage += LocalizationService_DeletedLanguage;
        }

        private void LocalizationService_DeletedLanguage(ILocalizationService sender, Umbraco.Core.Events.DeleteEventArgs<ILanguage> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.DeletedEntities)
            {
                LogHelper.Info<MacroHandler>("Delete: Deleting uSync File for item: {0}", () => item.CultureName);
                uSyncIOHelper.ArchiveRelativeFile(SyncFolder, item.CultureName.ToSafeAlias());
            }
        }

        private void LocalizationService_SavedLanguage(ILocalizationService sender, Umbraco.Core.Events.SaveEventArgs<ILanguage> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.SavedEntities)
            {
                LogHelper.Info<LanguageHandler>("Save: Saving uSync file for item: {0}", () => item.CultureName);
                ExportToDisk(item, uSyncBackOfficeContext.Instance.Configuration.Settings.Folder);
            }
        }
    }
}