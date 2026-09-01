import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface HealthResponse {
  status: string;
  application: string;
}

@Injectable({
  providedIn: 'root'
})
export class HealthService {

  constructor(
    private readonly http: HttpClient
  ) { }

  getHealth(): Observable<HealthResponse> {
    return this.http.get<HealthResponse>('/api/health');
  }
}
