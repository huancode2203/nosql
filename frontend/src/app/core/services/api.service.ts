import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

  get<T>(path: string, params?: Record<string, string | number | boolean>): Observable<ApiResponse<T>> {
    let query = new HttpParams();
    Object.entries(params || {}).forEach(([key, value]) => (query = query.set(key, String(value))));
    return this.http.get<ApiResponse<T>>(`${environment.apiUrl}${path}`, { params: query });
  }

  post<T>(path: string, body: unknown): Observable<ApiResponse<T>> {
    return this.http.post<ApiResponse<T>>(`${environment.apiUrl}${path}`, body);
  }

  put<T>(path: string, body: unknown): Observable<ApiResponse<T>> {
    return this.http.put<ApiResponse<T>>(`${environment.apiUrl}${path}`, body);
  }

  delete<T>(path: string): Observable<ApiResponse<T>> {
    return this.http.delete<ApiResponse<T>>(`${environment.apiUrl}${path}`);
  }

  getBlob(path: string): Observable<Blob> {
    return this.http.get(`${environment.apiUrl}${path}`, { responseType: 'blob' });
  }

  postForm<T>(path: string, form: FormData, params?: Record<string, string | number | boolean>): Observable<ApiResponse<T>> {
    let query = new HttpParams();
    Object.entries(params || {}).forEach(([key, value]) => (query = query.set(key, String(value))));
    return this.http.post<ApiResponse<T>>(`${environment.apiUrl}${path}`, form, { params: query });
  }
}
