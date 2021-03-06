﻿// Octopus MFS is an integrated suite for managing a Micro Finance Institution: 
// clients, contracts, accounting, reporting and risk
// Copyright © 2006,2007 OCTO Technology & OXUS Development Network
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License along
// with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
//
// Website: http://www.opencbs.com
// Contact: contact@opencbs.com

using System;
using System.Collections.Generic;
using System.Globalization;
using OpenCBS.CoreDomain;
using OpenCBS.Enums;
using OpenCBS.ExceptionsHandler;
using OpenCBS.ExceptionsHandler.Exceptions.CustomFieldExceptions;
using OpenCBS.Manager;
using OpenCBS.Shared;

namespace OpenCBS.Services
{
    public class CustomizableFieldsServices : MarshalByRefObject
	{
        private readonly CustomizableFieldsManager _customizableFieldsManager;

        public CustomizableFieldsServices(CustomizableFieldsManager collateralProductManager)
		{
            _customizableFieldsManager = collateralProductManager;
		}

		public CustomizableFieldsServices(User pUser)
		{
            _customizableFieldsManager = new CustomizableFieldsManager(pUser);
		}

        public CustomizableFieldsServices(string pTestDB)
		{
            _customizableFieldsManager = new CustomizableFieldsManager(pTestDB);
		}

        public List<string> SelectAllEntites()
        {
            return _customizableFieldsManager.SelectAllEntites();
        }

        public void AddCustomizableFields(List<CustomizableField> fields)
        {
            ValidateCustomFields(fields);
            _customizableFieldsManager.AddCustomizableFields(fields);
        }

        public void ValidateCustomFields(List<CustomizableField> fields)
        {
            foreach (CustomizableField field in fields)
            {
                if (field.Name.Contains(",") || field.Description.Contains(","))
                    throw new OpenCbsCustomFieldNameException(OCustomFieldExceptionEnum.FieldNameCanNotContainComma);
            }
        }

        public void UpdateCollectionField(CustomizableField field)
        {
            _customizableFieldsManager.DeleteCollection(field.Id);
            foreach (string value in field.Collection)
            {
                if (value.Trim().Length > 0)
                {
                    _customizableFieldsManager.AddCollectionItem(field.Id, value);
                }
            }
        }

        public List<CustomizableFieldSearchResult> SelectCreatedEntites()
        {
            return _customizableFieldsManager.SelectCreatedEntites();
        }

        public List<CustomizableField> SelectCustomizableFields(int entityId)
        {
            return _customizableFieldsManager.SelectCustomizableFields(entityId);
        }

        public bool CustomizableFieldsExistFor(OCustomizableFieldEntities entity)
        {
            return _customizableFieldsManager.CustomizableFieldsExistFor(entity);
        }

        public List<CustomizableFieldValue> CheckCustomizableFields(OCustomizableFieldEntities entity, CustomClass customFields, 
            CollectionList customizableFieldsCollections, int linkId, char linkType)
        {
            List<CustomizableFieldValue> fieldValues = new List<CustomizableFieldValue>();

            var customizableFieldsServices = ServicesProvider.GetInstance().GetCustomizableFieldsServices();
            List<CustomizableField> customizableFields = customizableFieldsServices.
                SelectCustomizableFields((int)Enum.Parse(typeof(OCustomizableFieldEntities), entity.ToString()));

            if (customizableFields != null)
            {
                foreach (CustomizableField field in customizableFields)
                {
                    CustomizableFieldValue customizableFieldValue = new CustomizableFieldValue {Field = field};

                    var fieldName = field.Name;
                    switch (field.Type)
                    {
                        case OCustomizableFieldTypes.Boolean:
                            customizableFieldValue.Value = ((bool) customFields.GetPropertyValueByName(fieldName)).ToString(CultureInfo.InvariantCulture);
                            break;
                        case OCustomizableFieldTypes.Number:
                            customizableFieldValue.Value = ((string)customFields.GetPropertyValueByName(fieldName));
                            break;
                        case OCustomizableFieldTypes.String:
                            customizableFieldValue.Value = (string)customFields.GetPropertyValueByName(fieldName);
                            break;
                        case OCustomizableFieldTypes.Date:
                            DateTime dateValue = (DateTime) customFields.GetPropertyValueByName(fieldName);
                            customizableFieldValue.Value = Converter.CustomFieldDateToString(dateValue);
                            break;
                        case OCustomizableFieldTypes.Client:
                            customizableFieldValue.Value = customFields.GetPropertyValueByName(fieldName).ToString();
                            break;
                        case OCustomizableFieldTypes.Collection:
                            {
                                int index = customizableFieldsCollections.GetItemIndexByName(fieldName, (string)customFields.GetPropertyValueByName(fieldName));
                                if (index != -1) customizableFieldValue.Value = index.ToString(CultureInfo.InvariantCulture);
                            }
                            break;
                    }

                    var fieldType = field.Type;
                    var fieldValue = customizableFieldValue.Value;
                    if (customizableFieldValue.Field.IsMandatory)
                        if (
                            (fieldType == OCustomizableFieldTypes.Number && fieldValue == string.Empty) ||
                            (fieldType == OCustomizableFieldTypes.String && fieldValue == string.Empty) ||
                            (fieldType == OCustomizableFieldTypes.Date && Converter.CustomFieldValueToDate(fieldValue) == DateTime.MinValue) ||
                            (fieldType == OCustomizableFieldTypes.Collection && fieldValue == null) ||
                            (fieldType == OCustomizableFieldTypes.Client && fieldValue == string.Empty)
                        )
                            throw new OpenCbsContractSaveException(OpenCbsContractSaveExceptionEnum.FieldIsMandatory);

                    if (fieldType == OCustomizableFieldTypes.Number)
                    {
                        if (fieldValue != string.Empty)
                        {
                            decimal result;
                            if (!Converter.CustomFieldDecimalParse(out result, fieldValue))
                                throw new OpenCbsContractSaveException(
                                    OpenCbsContractSaveExceptionEnum.NumberFieldIsNotANumber);
                        }
                    }

                    if (fieldType == OCustomizableFieldTypes.String)
                    {
                        if (fieldValue.Length>300)
                            throw new OpenCbsCustomFieldNameException(OCustomFieldExceptionEnum.FieldLimited);
                    }

                    if (field.IsUnique)
                    {
                        if (
                            fieldType == OCustomizableFieldTypes.Number ||
                            fieldType == OCustomizableFieldTypes.String ||
                            fieldType == OCustomizableFieldTypes.Date ||
                            fieldType == OCustomizableFieldTypes.Client
                        )
                        {
                            if (customizableFieldsServices.FieldValueExists(linkId, linkType, customizableFieldValue.Field.Id, fieldValue))
                                throw new OpenCbsContractSaveException(OpenCbsContractSaveExceptionEnum.FieldIsNotUnique);
                        }
                    }

                    fieldValues.Add(customizableFieldValue);
                }
            }
            return fieldValues;
        }

        public void SaveValues(OCustomizableFieldEntities entity, CustomClass customFields, CollectionList customizableFieldsCollections, int linkId, char linkType)
        {
            List<CustomizableFieldValue> fieldValues = CheckCustomizableFields(entity, customFields, customizableFieldsCollections, linkId, linkType);
            _customizableFieldsManager.SaveCustomizableFieldValues(fieldValues, linkId, linkType);
        }

        public List<CustomizableFieldValue> SelectCustomizableFieldValues(int linkId, char linkType)
        {
            return _customizableFieldsManager.SelectCustomizableFieldValues(linkId, linkType);
        }

        public bool FieldValueExists(int linkId, char linkType, int filedId, string value)
        {
            return _customizableFieldsManager.FieldValueExists(linkId, linkType, filedId, value);
        }

        public bool FieldValuesExistForFieldId(int fieldId)
        {
            return _customizableFieldsManager.FieldValuesExistForFieldId(fieldId);
        }

        public void DeleteField(CustomizableField field)
        {
            _customizableFieldsManager.DeleteField(field);
        }
	}
}
