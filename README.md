###What is this?
This repo includes a couple of episerver forms elements and actors I have written.
* Multiple file upload element for Episerver forms
* Actor to send email with uploaded files as attachments

###Installation
You can pretty much just copy the files to your project or you can follow these instructions to start a new Alloy project (episerver demo site) and install these features on that project:

1. Start new a new project of type "Episerver Web Site", name it "Alloy" (or change the namespaces...)
2. Copy the files in this folder into the web project (its important that the path to the views is /Views/Shared/ElementBlocks)
3. Build and run!

###Multiple file upload
A new formm element where the user can upload multiple files. The element plays nicely with Episerver file upload and Form submissions. The element is complete with validation for total files size and maximum number of files.
The element is not styled and looks like this on the Alloy Demo Site:
![alt tag](http://i.imgur.com/dG7HpM6.png)

Known problems
* The element does not format correctly if you are using "Placeholders" with the Episerver default SendEmailAfterSubmissionActor.
* There is no front end validation for the custom validators (on account of me being lazy)

###SendEmailWithFilesAsAttachment
This actor is a copy of the default Episerver SendEmailAfterSubmissionActor except it includes uploaded files as Attachments in the email.

This actor does not work with the multiple file upload element.

###SendEmailWithMultipleFilesAsAttachment
Thiss actor is a copy of SendEmailWithFilesAsAttachmentActor with fixes for the multiple file upload element (that is; all files are sent as attachments and placeholders work).
