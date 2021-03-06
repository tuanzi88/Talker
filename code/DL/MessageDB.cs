﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Sync;
using Microsoft.WindowsAzure.MobileServices.SQLiteStore;

using Talker.ML;
using Talker.DAL;

namespace Talker.DL
{
    public class MessageDB : IMessageDB
    {
        private static MessageDB mInstance = new MessageDB();
        private IMobileServiceSyncTable<Message> mMessageTable;

        public MessageDB()
            :base()
        {
            // Define table for local db
            DatabaseService.Instance.LocalDB.DefineTable<Message>();

            // Create user table for local db
            mMessageTable = DatabaseService.Instance.CloudDB.GetSyncTable<Message>();
        }

        public static MessageDB Instance
        {
            get 
            { 
                return mInstance;
            }
        }

        #region IMessageService implementation

        public async Task SyncAsync()
        {
            try 
            {
                await DatabaseService.Instance.CloudDB.SyncContext.PushAsync();
                await mMessageTable.PullAsync("allMessages", mMessageTable.CreateQuery()); // query ID is used for incremental sync
            }
            catch (MobileServiceInvalidOperationException e) {
                Debug.WriteLine(@"Sync Failed: {0}", e.Message);
            }
        }

        public async Task RefreshDataAsync(string pReceiverID)
        {
            try 
            {
                // update the local store
                // all operations on messageTable use the local database, call SyncAsync to send changes
                await SyncAsync();                          

                // This code refreshes the entries in the list view by querying the local table.
                GlobalManager.Instance.CurrentMessages.Clear();
                GlobalManager.Instance.CurrentMessages = await mMessageTable
                    .Where (message => message.ReceiverID == pReceiverID).ToListAsync ();

            } 
            catch (MobileServiceInvalidOperationException e) 
            {
                Debug.WriteLine (@"ERROR {0}", e.Message);
            }
        }

        public async Task InsertMessageAsync(Message pMessage)
        {
            try 
            {
                await mMessageTable.InsertAsync (pMessage); // Insert a new TodoItem into the local database. 
                await SyncAsync(); // send changes to the mobile service

                GlobalManager.Instance.CurrentMessages.Add (pMessage); 
            } 
            catch (MobileServiceInvalidOperationException e) 
            {
                Debug.WriteLine (@"ERROR {0}", e.Message);
            }
        }

        public async Task DeleteMessage(string pMessageID)
        {
            foreach (Message one in GlobalManager.Instance.CurrentMessages)
            {
                if (one.ID == pMessageID)
                {
                    try
                    {
                        await mMessageTable.DeleteAsync(one);
                        await SyncAsync();

                        GlobalManager.Instance.CurrentMessages.Remove(one);
                    }
                    catch (MobileServiceInvalidOperationException e) 
                    {
                        Debug.WriteLine (@"ERROR {0}", e.Message);
                    }
                }
            }
        }
        #endregion
    }
}

