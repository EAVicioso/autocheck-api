Feature: CU-10/11/13 secure queries and RS-10 logout
  Scenario: Query archived vehicles explicitly
    Given an owned archived vehicle
    When its owner lists vehicles with incluirArchivados=true
    Then it appears without changing its archive state
  Scenario: Seek audit events with an immutable cursor
    Given four ordered audit events and a page size of two
    When a global auditor follows siguienteCursor
    Then the next page has no repeated event
    And a cursor with a different order or direction is rejected with 400
  Scenario: Record a permission rejection
    Given a citizen with a valid session
    When the citizen requests the permission catalog
    Then the server returns 403 and appends a sanitized immutable security event
  Scenario: Retry logout after local revocation
    Given an own cryptographically valid token for a revoked session
    When logout is retried
    Then the server returns 204 and other protected operations return 401
    And the first local revocation has exactly one LOGOUT event
