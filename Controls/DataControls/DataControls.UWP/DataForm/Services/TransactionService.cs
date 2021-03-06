﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telerik.Data.Core;
using Telerik.UI.Xaml.Controls.Data.DataForm.Commands;
using Telerik.UI.Xaml.Controls.Primitives;

namespace Telerik.UI.Xaml.Controls.Data.DataForm
{
    public class TransactionService : ServiceBase<RadDataForm>, ITransactionService
    {
        private Dictionary<string, List<object>> errors = new Dictionary<string, List<object>>();

        internal TransactionService(RadDataForm owner)
            : base(owner)
        {
            if (this.Owner == null)
            {
                throw new ArgumentNullException("Transaction service cannot operate without owner");
            }
        }

        internal bool CommitPropertyCore(EntityProperty property)
        {
            if (this.Owner.ValidationMode == ValidationMode.OnCommit)
            {
                if (this.ValidateProperty(property))
                {
                    property.Commit();
                    return true;
                }
            }
            else
            {
                if (this.errors.ContainsKey(property.PropertyName))
                {
                    if (this.errors[property.PropertyName].Count == 0)
                    {
                        property.Commit();
                        return true;
                    }
                }
                else
                {
                    property.Commit();
                    return true;
                }
            }

            return false;
        }

        public bool CommitProperty(string propertyName)
        {
            var entityProperty = this.Owner.Entity.GetEntityProperty(propertyName);
            if (entityProperty != null)
            {
                return this.CommitPropertyCore(entityProperty);
            }
            return false;
        }

        public bool CommitAll()
        {
            if (this.ValidateAll())
            {
                foreach (var entityProperty in this.Owner.Entity.Properties)
                {
                    this.CommitPropertyCore(entityProperty);
                }
                return true;
            }

            return false;
        }

        public async Task<bool> ValidatePropertyAsync(string propertyName)
        {
            var isValid = true;

            var validator = this.Owner.Entity.Validator;
            if (validator != null)
            {
                var task = validator.ValidatePropertyAsync(this.Owner.Entity, propertyName);
                task.Start();
                await task;

                isValid = validator.GetErrors(propertyName).OfType<string>().ToList().Count == 0;
            }

            this.Owner.InvokeAsync(()=> UpdateEntityPropertyDisplayMessage(isValid, propertyName));

            return isValid;
        }

        private void UpdateEntityPropertyDisplayMessage(bool isValid, string propertyName)
        {
            var property = this.Owner.Entity.GetEntityProperty(propertyName);

            if (property != null)
            {
                property.DisplayPositiveMessage = isValid;
            }
        }

        public async Task<bool> ValidateAllAsync()
        {
            bool isValid = true;
            var validator = this.Owner.Entity.Validator;
            foreach (var property in this.Owner.Entity.Properties)
            {
                var task = this.ValidatePropertyAsync(property.PropertyName);
                var isPropertyValid = await task;
                isValid = isValid && isPropertyValid;
            }

            return isValid;
        }

        internal bool ValidateProperty(EntityProperty property)
        {
            bool isValid = true;

            var validator = this.Owner.Entity.Validator;
            if (validator != null)
            {
                var task = validator.ValidatePropertyAsync(this.Owner.Model.Entity, property.PropertyName);
                if (task != null)
                {
                    task.Start();
                    task.Wait();
                }

                isValid = validator.GetErrors(property.PropertyName).OfType<string>().ToList().Count == 0;
            }

            this.Owner.InvokeAsync(() => UpdateEntityPropertyDisplayMessage(isValid, property.PropertyName));

            return isValid;
        }

        internal bool ValidateAll()
        {
            var entity = this.Owner.Entity;

            var isValid = true;

            foreach (var property in entity.Properties)
            {
                this.Owner.CommandService.ExecuteCommand(CommandId.Validate, property);

                isValid = isValid & property.IsValid;
            }

            if (entity.Validator != null)
            {
                return !this.Owner.Entity.Validator.HasErrors;
            }
            else
            {
                return isValid;
            }
        }

        void ITransactionService.ValidateProperty(EntityProperty property)
        {
            this.ValidateProperty(property);
        }

        void ITransactionService.CommitPropertyCore(EntityProperty property)
        {
            this.CommitPropertyCore(property);
        }

        void ITransactionService.ErrorsChanged(object sender, string propertyName)
        {
            var property = this.Owner.Entity.GetEntityProperty(propertyName);
            var list = (sender as ISupportEntityValidation).GetErrors(propertyName).OfType<object>().ToList();
            this.errors[propertyName] = list;

            var _ = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    var errorsList = (sender as ISupportEntityValidation).GetErrors(propertyName).OfType<object>().ToList();

                    property.Errors.Clear();

                    foreach (var error in errorsList)
                    {
                        property.Errors.Add(error);
                    }
                });
        }
    }
}
