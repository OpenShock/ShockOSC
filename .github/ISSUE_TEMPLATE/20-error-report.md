name: 'Error Report'
description: "An unhandled error occoured in ShockOSC"
title: '[Error] '
labels: ['type: error', 'status: triage']
projects: ['OpenShock/6']

body:

  - type: markdown
    attributes:
      value: |
        # Checklist

  - type: checkboxes
    id: checklist
    attributes:
      label: Pre-submission checklist
      description: |
        To prevent wasting your or our time, please fill out the below checklist before continuing.
        Thanks for understanding!
      options:
        - label: 'I checked that no other Error or Bug Report describing my problem exists.'
          required: true
        - label: 'I am running the latest stable or prerelease version of ShockOSC.'
          required: true
        - label: 'I accept that this issue may be closed if any of the above are found to be untrue.'
          required: true

  - type: markdown
    attributes:
      value: |
        # Board & Firmware

  - type: dropdown
    id: os
    attributes:
      label: OS
      description: What Operating System are you running?
      options:
        - Windows 10
        - Windows 11
        - Linux
        - Other (please mention below)
    validations:
      required: True

  - type: markdown
    attributes:
      value: |
        # Error / Exception Stack Trace

  - type: textarea
    id: exception
    attributes:
      label: 'Paste your error / exception stack trace here'
    validations:
      required: true

  - type: input
    id: shockosc-version
    attributes:
      label: 'ShockOSC version'
      description: Which ShockOSC version did you use?
      placeholder: 'E.g.: 1.2.4, 1.0.0-rc.4..'
    validations:
      required: true

  - type: textarea
    id: what-happened
    attributes:
      label: 'Describe what you were doing when the error occoured as precisely as possible.'
    validations:
      required: true

  - type: markdown
    attributes:
      value: |
        # Steps to reproduce

  - type: textarea
    id: how-to-reproduce
    attributes:
      label: 'Reproduction Steps'
      description: 'If you can reproduce the error, describe the exact steps you took to make the problem appear.'
        
    validations:
      required: false

  - type: markdown
    attributes:
      value: |
        # Anything else?

  - type: textarea
    id: anything-else
    attributes:
      label: 'Other remarks'
    validations:
      required: false
