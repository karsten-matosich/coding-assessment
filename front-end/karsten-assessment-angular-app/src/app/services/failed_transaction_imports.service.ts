import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from './api.config';

export interface FailedTransactionImport {
  id: number;
  transaction_upload_id: number;
  external_transaction_id: string;
  error_message: string;
  csv_row_value: string;
}

@Injectable({ providedIn: 'root' })
export class FailedTransactionImportsService {
  constructor(private http: HttpClient) {}

  getFailedTransactionImports(): Observable<FailedTransactionImport[]> {
    return this.http.get<FailedTransactionImport[]>(`${API_URL}/failed_transaction_imports/get_all`);
  }
}