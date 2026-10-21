# Account opening documents

A positive opening balance is a cash deposit. The server links it to the signed-in user's activated open teller session for the active posting business date, adds a Deposit movement, and increases expected session cash. A missing or ineligible session returns `CashSessionNotOpen`, keeping the creation form open. Zero opening balances require no session and create no cash movement. The client does not select or send a session ID. This applies to new openings; historical deposits are unchanged.

After holder lookup, the client loads account-opening options, including the backend-selected reusable customer documents. For joint accounts either holder may supply each type; selection prefers the primary holder and then the highest eligible document ID. Files must be present, readable, compatible with account upload rules, and unexpired in Myanmar business time. Verified customer KYC is still required.

The document step displays each covered requirement as “Already uploaded — will be copied,” with its holder and document number. Upload buttons appear only for uncovered requirements. When every requirement is covered, no upload is requested; product settings remain available on the same step.

Creation submits either `Documents[i].SourceCustomerDocumentId` or `Documents[i].File`. The server independently validates sources and creates private account copies, initially pending verification. Later customer-document changes do not affect those copies.

Holder changes clear previous availability and uploads; late lookup/options responses are ignored. Product changes rebuild the document requirements. If submission returns `ReusableAccountDocumentUnavailable` (3134), refresh options, preserve valid local uploads, return to the documents step, and show the error without leaving the form.

Deploy the server and `ReuseCustomerDocumentsForAccountOpening` migration before this desktop version. Existing account documents have no source ID. Existing upload-only clients remain supported.
