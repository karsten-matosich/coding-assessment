# Decisions

- I initially considered using SQL Server because I was more familiar with it, but 
SQLite was a lot faster to setup, easy to create tests for, and ended up being a better 
fit for a smaller project like this.

- I debated on whether or not to use views for the dead-letter mechanism, and I think
in a production environment I would still go that route, but for simplicity I just 
created the failed_transaction_uploads table instead.

- Unit tests were initially synchronous, because it was easier to write that way, with
a little help from AI I was able to get them to run in parallel.

- Tests were written only/explicitly for insert/update functionality, and exclusively
for the back-end routes (what I would consider to be the core logic for this app)

- Opted not to test the private functions in the routes files.

- I built the majority of the account component from scratch, and then had AI use it as
a template to generate the other components. I also created the account service and used
it as a template to generate the others. Both myself and AI made tweaks after the initial
creations/generations.

- Some front-end functionality has intentionally been sacrificed in attempt to 'minimal'
and 'simple' as instructed in the assessment overview. For example, only tables that were 
becoming 'large' (in my opinion, no real metric here) have pagination. Table filters
and search functitonality is also non-existent. Honestly, I still probably went a little
overboard.

- Golden CSV reconciliation report is UI only, no data is stored. In production, I would
have made the result downloadable, or stored it in a table.

- Upload file size has been hard-capped at 50mb, and I only tested a csv with 500 records.
In a production environment with potentially tens of thousands of transactions, I may have
set a max records returned for the route, limited it to a certain specified date range, made
the table only show transactions for one account at a time etc. to improve performance.

- I had AI do a final pass over all major directories to check for unused code, duplicate
functionality, and other refactoring opportunities. I manually QA tested all the AI changes.