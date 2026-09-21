# API Contracts

## Create Account

`POST /api/accounts` consumes `multipart/form-data`.

Account fields retain their existing names. Documents use indexed keys:

```text
Documents[0].DocumentType=Nrc
Documents[0].DocumentNumber=optional-number
Documents[0].File=<binary file>
```

Repeat the indexed group for each required document. Each document type may appear once. Supported file formats are PDF, JPEG, and PNG, with a maximum size of 10 MB per file.

The complete multipart request is limited to 60 MB by default.

The six seeded account types are Current, Normal Saving, Special Saving, Normal Deposit, Special Deposit, and Hundred-Days Deposit. Each requires NRC, Photo, ProofOfAddress, HouseholdRegistration, and SourceOfFunds documents.

Files are stored beneath the configured `FileUploads:RootPath` with GUID-generated names. The API does not expose a public document-download endpoint.
