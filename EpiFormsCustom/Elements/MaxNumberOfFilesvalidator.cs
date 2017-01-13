using System.Collections.Generic;
using System.Linq;
using System.Web;
using EPiServer.Forms.Core.Models;
using EPiServer.Forms.Core.Validation;
using EPiServer.Forms.Implementation.Validation;

namespace Alloy.EpiFormsCustom.Elements
{
    public class MaxNumberOfFilesValidator : InternalElementValidatorBase
    {
        /// <inheritdoc />
        public override bool? Validate(IElementValidatable targetElement)
        {
            MultipleFileUploadElementBlock uploadElementBlock = targetElement as MultipleFileUploadElementBlock;
            if (uploadElementBlock == null)
                return true;
            IEnumerable<HttpPostedFile> submittedValue = targetElement.GetSubmittedValue() as IEnumerable<HttpPostedFile>;
            if (submittedValue == null || submittedValue.Count() == 0)
                return true;
            var flag = true;

            var numberOfFiles = submittedValue.Count();
            if (numberOfFiles > uploadElementBlock.MaxNumberOfFiles)
            {
                flag = false;
            }
            return flag;
        }

        /// <inheritdoc />
        public override IValidationModel BuildValidationModel(IElementValidatable targetElement)
        {
            MultipleFileUploadElementBlock uploadElementBlock = targetElement as MultipleFileUploadElementBlock;
            if (uploadElementBlock == null)
                return base.BuildValidationModel(targetElement);
            if (this._model == null)
            {
                MaxNumberOfFilesValidationModel sizeValidationModel = new MaxNumberOfFilesValidationModel();
                sizeValidationModel.MaxNoOfFiles = uploadElementBlock.MaxNumberOfFiles;
                string str = string.Format(this._validationService.Service.GetValidatorMessage(this.GetType(), ""), uploadElementBlock.MaxNumberOfFiles);
                sizeValidationModel.Message = str;
                this._model = (IValidationModel)sizeValidationModel;
            }
            return this._model;
        }
    }

    public class MaxNumberOfFilesValidationModel : ValidationModelBase
    {
        public int MaxNoOfFiles { get; set; }
    }
}