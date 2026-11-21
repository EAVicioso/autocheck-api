Feature: DEC-LIFECYCLE-01 and CU-11 / PA-11 / RS-03
  Scenario: Archive a vehicle without pending work
    Given an active citizen owns a vehicle at version 1
    When the citizen deletes the vehicle with version 1
    Then the response is 204 and the vehicle identity and history remain
    And one immutable VEHICULO_ARCHIVADO event exists
  Scenario: Reject an obsolete profile update
    Given the profile version has advanced to 2
    When its owner submits a profile update with version 1
    Then the response is 409 VERSION_DESACTUALIZADA
    And no profile or audit mutation commits
  Scenario: Archive another user
    Given an authorized administrator and a user without pending work
    When the administrator archives that user with the current version
    Then the response is 204 and all local sessions are revoked
    And the Supabase identity mapping remains
  Scenario: Deny direct permission catalog access
    Given a citizen with a valid session
    When the citizen reads the permission catalog
    Then the response is 403 and no permission changes
