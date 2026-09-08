"""Authenticate a completed recovery controller without granting publication authority."""

from publication import PublicationError, _authorize_ci_invocation, positive_integer


def authorize_completed_controller(repository, workflow, run, run_id, attempt, jobs, required_check, comparison):
    controller = _authorize_ci_invocation(repository, workflow, run, run_id, attempt, jobs, required_check, comparison,
                                          'workflow_dispatch')
    expected = {'build': 'success', 'js / local': 'success', 'js / main': 'skipped',
                'js / complete': 'success', 'register-publication': 'success'}
    for name, conclusion in expected.items():
        selected = [job for job in jobs if job.get('name') == name]
        if len(selected) != 1:
            raise PublicationError('Recovery invocation lacks exact full-route job evidence')
        job = selected[0]
        if not all(positive_integer(job.get(key)) for key in ('id', 'run_id', 'run_attempt')) or any(
            job.get(key) != value for key, value in {'run_id': run_id, 'run_attempt': attempt,
                                                   'head_sha': controller.source_sha, 'status': 'completed',
                                                   'conclusion': conclusion}.items()
        ):
            raise PublicationError('Recovery job does not prove the exact credential-free full validation attempt')
    return controller
